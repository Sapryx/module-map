using Microsoft.CodeAnalysis;
using ModuleMap.Analysis;

namespace ModuleMap.Grouping;

public sealed class ModuleGrouper
{
    private static readonly string[] ViewModelSuffixes = ["ViewModel", "Vm"];
    private static readonly string[] ViewSuffixes = ["View", "Window", "Control", "Page", "Dialog"];

    public static string Stem(string viewModelName)
    {
        string? suffix = ViewModelSuffixes.FirstOrDefault(it => viewModelName.EndsWith(it, StringComparison.Ordinal));
        return suffix == null ? viewModelName : viewModelName.Substring(0, viewModelName.Length - suffix.Length);
    }

    public List<ModuleGroup> Group(SourceIndex index)
    {
        var owners = new Dictionary<SourceUnit, ModuleGroup>();
        var groups = new List<ModuleGroup>();
        var features = new Dictionary<SourceUnit, ModuleGroup>();
        foreach(var unit in index.Units.Where(IsViewModel))
        {
            var group = new ModuleGroup() { Project = unit.Project, Namespace = unit.Namespace, ViewModel = unit };
            features[unit] = group;
            groups.Add(group);
            Assign(unit, group, owners);
        }

        foreach(var unit in index.Units.Where(it => !owners.ContainsKey(it)).ToList())
        {
            var feature = features.FirstOrDefault(it => IsViewOf(unit, it.Key, index)).Value;
            if(feature != null)
            {
                Assign(unit, feature, owners);
            }
        }

        var namespaces = new Dictionary<string, ModuleGroup>();
        foreach(var unit in index.Units.Where(it => !owners.ContainsKey(it)).ToList())
        {
            string key = NamespaceKey(unit);
            if(!namespaces.TryGetValue(key, out var group))
            {
                group = new ModuleGroup() { Project = unit.Project, Namespace = unit.Namespace };
                namespaces[key] = group;
                groups.Add(group);
            }

            Assign(unit, group, owners);
        }

        AttachRemainders(features.Values, namespaces, owners, index);
        return groups.Where(it => it.Units.Count > 0).ToList();
    }

    private static bool IsViewModel(SourceUnit unit)
    {
        return unit.Types.Values.Any(it => it.TypeKind == TypeKind.Class
            && !it.IsAbstract
            && !it.IsStatic
            && it.Name == unit.Name
            && ViewModelSuffixes.Any(suffix => it.Name.Length > suffix.Length
                && it.Name.EndsWith(suffix, StringComparison.Ordinal)));
    }

    private static bool IsViewOf(SourceUnit unit, SourceUnit viewModel, SourceIndex index)
    {
        if(unit.RootDataTypes.Count > 0)
        {
            return unit.RootDataTypes.Any(it => index.FindByType(it) == viewModel);
        }

        string stem = Stem(viewModel.Name);
        return unit.Types.Count > 0 && ViewSuffixes.Any(it => unit.Name == stem + it);
    }

    private static string NamespaceKey(SourceUnit unit)
    {
        return unit.Project.Name + "|" + unit.Namespace;
    }

    private static void Assign(SourceUnit unit, ModuleGroup group, Dictionary<SourceUnit, ModuleGroup> owners)
    {
        group.Units.Add(unit);
        owners[unit] = group;
    }

    private static void AttachRemainders(
        IEnumerable<ModuleGroup> features,
        Dictionary<string, ModuleGroup> namespaces,
        Dictionary<SourceUnit, ModuleGroup> owners,
        SourceIndex index
    )
    {
        var split = features
            .SelectMany(it => it.Units)
            .Select(it => namespaces.GetValueOrDefault(NamespaceKey(it)))
            .OfType<ModuleGroup>()
            .Distinct()
            .ToList();
        var incoming = new Dictionary<SourceUnit, List<SourceUnit>>();
        foreach(var unit in index.Units)
        {
            foreach(var target in unit.Links.Keys)
            {
                if(!incoming.TryGetValue(target, out var sources))
                {
                    sources = [];
                    incoming[target] = sources;
                }

                sources.Add(unit);
            }
        }

        foreach(var group in split)
        {
            foreach(var unit in group.Units.ToList())
            {
                var neighbors = unit.Links.Keys.Concat(incoming.GetValueOrDefault(unit, []));
                var best = OnlyFeature(neighbors, owners);
                if(best != null)
                {
                    group.Units.Remove(unit);
                    Assign(unit, best, owners);
                }
            }
        }
    }

    private static ModuleGroup? OnlyFeature(
        IEnumerable<SourceUnit> neighbors,
        Dictionary<SourceUnit, ModuleGroup> owners
    )
    {
        var features = new HashSet<ModuleGroup>();
        foreach(var neighbor in neighbors)
        {
            var owner = owners.GetValueOrDefault(neighbor);
            if(owner?.ViewModel != null)
            {
                features.Add(owner);
            }
        }

        return features.Count == 1 ? features.First() : null;
    }
}
