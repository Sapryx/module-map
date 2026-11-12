using Microsoft.CodeAnalysis;
using ModuleMap.Analysis;
using ModuleMap.Grouping;
using ModuleMap.Repository;
using ModuleMap.Workspace;

namespace ModuleMap.Map;

public sealed class MapBuilder
{
    private const int ListLimit = 6;
    private readonly GitRepository repository;
    private readonly LayoutEngine layout;
    private readonly ModuleClassifier classifier;
    private readonly NoteWriter noteWriter;
    private readonly double hiddenShare;

    public MapBuilder(
        GitRepository repository,
        LayoutEngine layout,
        ModuleClassifier classifier,
        NoteWriter noteWriter,
        double hiddenShare
    )
    {
        this.repository = repository;
        this.layout = layout;
        this.classifier = classifier;
        this.noteWriter = noteWriter;
        this.hiddenShare = hiddenShare;
    }

    public MapDocument Build(
        LoadedSolution solution,
        List<ModuleGroup> groups,
        PackageIndex packages,
        string name
    )
    {
        var drafts = CreateDrafts(groups);
        Connect(drafts, packages);
        MarkRoots(drafts);
        var hiddenPackages = HideUbiquitous(drafts);
        AssignEdgeTypes(drafts);
        classifier.Classify(drafts);
        var projects = OrderProjects(solution, drafts);
        var rows = layout.Arrange(drafts, projects);
        var externals = Externals(drafts);
        return new MapDocument()
        {
            Title = $"{name} module map",
            Lede = "Generated from the code by the C# analyzer: each view model forms a feature module with the views "
                + "bound to it, and every other namespace is a module. Every orange wire crosses a project boundary.",
            Commit = repository.Commit(),
            Date = DateTime.Now.ToString("yyyy-MM-dd"),
            PackageKind = "NuGet package",
            NotDrawn = NotDrawn(drafts, hiddenPackages),
            Projects = projects.Select(it => Project(solution, it)).ToList(),
            Rows = rows,
            Modules = drafts.Select(Module).ToList(),
            Externals = externals,
            Edges = Edges(drafts, externals),
            Notes = noteWriter.Write(drafts)
        };
    }

    private static List<ModuleDraft> CreateDrafts(List<ModuleGroup> groups)
    {
        var names = groups.Select(BaseName).ToList();
        var drafts = new List<ModuleDraft>();
        var ids = new HashSet<string>();
        for(int i = 0; i < groups.Count; i++)
        {
            string displayName = names.Count(it => it == names[i]) > 1
                ? $"{names[i]} ({groups[i].Project.Name})"
                : names[i];
            string id = Naming.Kebab(displayName);
            string uniqueId = id;
            for(int suffix = 2; !ids.Add(uniqueId); suffix++)
            {
                uniqueId = id + "-" + suffix;
            }

            drafts.Add(new ModuleDraft() { Group = groups[i], Id = uniqueId, Name = displayName });
        }

        return drafts;
    }

    private static string BaseName(ModuleGroup group)
    {
        if(group.ViewModel != null)
        {
            return Naming.Words(ModuleGrouper.Stem(group.ViewModel.Name));
        }

        string ns = group.Namespace.Length == 0 ? group.Project.Name : group.Namespace;
        return ns.Substring(ns.LastIndexOf('.') + 1);
    }

    private static void Connect(List<ModuleDraft> drafts, PackageIndex packages)
    {
        var byUnit = new Dictionary<SourceUnit, ModuleDraft>();
        foreach(var draft in drafts)
        {
            foreach(var unit in draft.Group.Units)
            {
                byUnit[unit] = draft;
            }
        }

        foreach(var draft in drafts)
        {
            foreach(var unit in draft.Group.Units)
            {
                foreach(var pair in unit.Links)
                {
                    var target = byUnit.GetValueOrDefault(pair.Key);
                    if(target != null && target != draft)
                    {
                        var edge = EdgeTo(draft, target);
                        edge.Via.UnionWith(pair.Value.TypeNames);
                        edge.Implements |= pair.Value.Implements;
                        edge.Listens |= pair.Value.Listens;
                    }
                }

                foreach(string assembly in unit.ExternalAssemblies)
                {
                    var package = packages.Find(unit.Project, assembly);
                    if(package != null)
                    {
                        draft.Packages[package.Id] = package;
                    }
                }

                draft.Natives.UnionWith(unit.NativeLibraries);
            }
        }
    }

    private static ModuleEdge EdgeTo(ModuleDraft source, ModuleDraft target)
    {
        if(!source.Outgoing.TryGetValue(target, out var edge))
        {
            edge = new ModuleEdge() { From = source, To = target };
            source.Outgoing[target] = edge;
            target.Incoming.Add(edge);
        }

        return edge;
    }

    private static void MarkRoots(List<ModuleDraft> drafts)
    {
        foreach(var draft in drafts)
        {
            draft.IsRoot = draft.Group.Units.Any(it => it.IsEntry)
                || draft.Types.Any(it => it.Name == "CompositionRoot");
        }
    }

    private List<string> HideUbiquitous(List<ModuleDraft> drafts)
    {
        double threshold = Math.Max(3, (drafts.Count - 1) * hiddenShare);
        foreach(var draft in drafts.Where(it => !it.IsRoot && it.Group.ViewModel == null))
        {
            int users = draft.Incoming.Count(it => !it.From.IsRoot);
            draft.IsHidden = users >= threshold;
        }

        var hidden = drafts
            .SelectMany(it => it.Packages.Keys)
            .GroupBy(it => it)
            .Where(it => it.Count() >= threshold)
            .Select(it => it.Key)
            .Order(StringComparer.Ordinal)
            .ToList();
        foreach(var draft in drafts)
        {
            foreach(string id in hidden)
            {
                draft.Packages.Remove(id);
            }
        }

        return hidden;
    }

    private static void AssignEdgeTypes(List<ModuleDraft> drafts)
    {
        foreach(var edge in drafts.SelectMany(it => it.Outgoing.Values))
        {
            bool mutual = edge.To.Outgoing.ContainsKey(edge.From) && !edge.To.IsRoot;
            edge.Type = edge.From.IsRoot ? "wiring"
                : mutual && edge.IsVisible && !edge.From.IsHidden ? "cycle"
                : edge.Implements ? "implements"
                : edge.Listens ? "listens"
                : "uses";
        }
    }

    private static List<string> OrderProjects(LoadedSolution solution, List<ModuleDraft> drafts)
    {
        var used = drafts.Select(it => it.Project).ToHashSet();
        var depths = new Dictionary<string, int>();
        foreach(var project in solution.Projects)
        {
            depths[project.Name] = Depth(solution, project, []);
        }

        return used
            .OrderByDescending(it => depths.GetValueOrDefault(it))
            .ThenBy(it => it, StringComparer.Ordinal)
            .ToList();
    }

    private static int Depth(LoadedSolution solution, Project project, HashSet<ProjectId> visiting)
    {
        if(!visiting.Add(project.Id))
        {
            return 0;
        }

        int depth = References(solution, project)
            .Select(it => Depth(solution, it, visiting) + 1)
            .DefaultIfEmpty(0)
            .Max();
        visiting.Remove(project.Id);
        return depth;
    }

    private static IEnumerable<Project> References(LoadedSolution solution, Project project)
    {
        return project.ProjectReferences
            .Select(it => solution.Solution.GetProject(it.ProjectId))
            .OfType<Project>()
            .Where(solution.Projects.Contains);
    }

    private MapProject Project(LoadedSolution solution, string name)
    {
        var project = solution.Projects.First(it => it.Name == name);
        string directory = repository.ToRelative(Path.GetDirectoryName(project.FilePath ?? "") ?? "");
        var references = References(solution, project).Select(it => it.Name).Order(StringComparer.Ordinal).ToList();
        string summary = references.Count == 0
            ? "no project references"
            : "references " + string.Join(", ", references);
        return new MapProject() { Id = name, Title = name, Sub = $"{directory} · {summary}" };
    }

    private static string NotDrawn(List<ModuleDraft> drafts, List<string> hiddenPackages)
    {
        var hidden = drafts.Where(it => it.IsHidden).Select(it => $"<code>{it.Name}</code>").ToList();
        var parts = hidden.Concat(hiddenPackages.Select(it => $"<code>{it}</code>")).ToList();
        string common = parts.Count == 0
            ? ""
            : $"Wires to {string.Join(", ", parts)}, which most modules use, are not drawn. ";
        return common + "The .NET base library is not drawn either.";
    }

    private static MapModule Module(ModuleDraft draft)
    {
        var types = draft.Types;
        var used = draft.Incoming.SelectMany(it => it.Via).ToHashSet();
        var iface = types
            .Where(it => used.Contains(it.Name))
            .OrderBy(it => it.TypeKind == TypeKind.Interface ? 0 : 1)
            .ThenBy(it => it.Name, StringComparer.Ordinal)
            .Select(it => it.Name)
            .ToList();
        if(iface.Count == 0)
        {
            iface = types
                .Where(it => it.DeclaredAccessibility == Accessibility.Public)
                .Select(it => it.Name)
                .Order(StringComparer.Ordinal)
                .ToList();
        }

        var impl = types.Select(it => it.Name).Where(it => !iface.Contains(it)).Order(StringComparer.Ordinal).ToList();
        var libs = draft.Packages.Keys
            .Order(StringComparer.Ordinal)
            .Concat(draft.Natives.Order(StringComparer.Ordinal));
        return new MapModule()
        {
            Id = draft.Id,
            Name = draft.Name,
            Kind = draft.Kind,
            Project = draft.Project,
            Ns = string.Join(", ", draft.Group.Units.Select(it => it.Namespace).Distinct().Order()),
            Col = draft.Col,
            Row = draft.Row,
            Portability = draft.Portability,
            Iface = iface.Count == 0 ? "markup only" : Naming.List(iface, ListLimit),
            Impl = impl.Count == 0 ? "none" : Naming.List(impl, ListLimit),
            Libs = string.Join(", ", libs),
            Note = Note(draft, types),
            Files = draft.Group.Units.SelectMany(it => it.Files).Order(StringComparer.Ordinal).ToList()
        };
    }

    private static string Note(ModuleDraft draft, List<INamedTypeSymbol> types)
    {
        string summary;
        if(draft.IsRoot)
        {
            summary = "Entry point and composition root: creates and wires the other modules.";
        }
        else if(draft.Group.ViewModel != null)
        {
            var views = draft.Group.Units.Where(it => it != draft.Group.ViewModel).Select(it => it.Name).ToList();
            summary = views.Count == 0
                ? $"View model {draft.Group.ViewModel.Name} without views of its own."
                : $"View model {draft.Group.ViewModel.Name} with {Naming.List(views, ListLimit)}.";
        }
        else
        {
            summary = types.Count == 0
                ? $"Markup in {draft.Group.Namespace}."
                : $"{types.Count} types in {draft.Group.Namespace}.";
        }

        var users = draft.Incoming
            .Where(it => !it.From.IsRoot)
            .Select(it => it.From.Name)
            .Order(StringComparer.Ordinal);
        string usedBy = users.Any() ? $" Used by {Naming.List(users, 4)}." : "";
        return summary + usedBy;
    }

    private static List<MapExternal> Externals(List<ModuleDraft> drafts)
    {
        var packages = drafts
            .SelectMany(draft => draft.Packages.Values
                .Select(package => new KeyValuePair<PackageInfo, ModuleDraft>(package, draft)))
            .GroupBy(it => it.Key.Id)
            .OrderBy(it => it.Key, StringComparer.Ordinal)
            .Select(it => new MapExternal()
            {
                Id = "x-" + Naming.Kebab(it.Key),
                Name = it.Key,
                Sub = "version " + it.First().Key.Version,
                Kind = "package",
                Projects = string.Join(", ", it.Select(pair => pair.Value.Project).Distinct().Order()),
                Note = $"NuGet package used by {Naming.List(it.Select(pair => pair.Value.Name), 4)}."
            });
        var natives = drafts
            .SelectMany(draft => draft.Natives.Select(native => new KeyValuePair<string, ModuleDraft>(native, draft)))
            .GroupBy(it => it.Key)
            .OrderBy(it => it.Key, StringComparer.Ordinal)
            .Select(it => new MapExternal()
            {
                Id = "x-native-" + Naming.Kebab(it.Key),
                Name = it.Key,
                Sub = "native library",
                Kind = "native",
                Projects = string.Join(", ", it.Select(pair => pair.Value.Project).Distinct().Order()),
                Note = $"Called through P/Invoke by {Naming.List(it.Select(pair => pair.Value.Name), 4)}."
            });
        return packages.Concat(natives).ToList();
    }

    private static List<List<string>> Edges(List<ModuleDraft> drafts, List<MapExternal> externals)
    {
        var edges = drafts
            .SelectMany(it => it.Outgoing.Values)
            .Where(it => it.IsVisible)
            .OrderBy(it => it.From.Id, StringComparer.Ordinal)
            .ThenBy(it => it.To.Id, StringComparer.Ordinal)
            .Select(it => new List<string>()
            {
                it.From.Id,
                it.To.Id,
                it.Type,
                Naming.List(it.Via.Order(StringComparer.Ordinal), ListLimit)
            })
            .ToList();
        var ids = externals.ToDictionary(it => it.Name, it => it.Id);
        foreach(var draft in drafts)
        {
            foreach(string name in draft.Packages.Keys.Concat(draft.Natives).Order(StringComparer.Ordinal))
            {
                edges.Add([draft.Id, ids[name], "external"]);
            }
        }

        return edges;
    }
}
