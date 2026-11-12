namespace ModuleMap.Map;

public sealed class NoteWriter
{
    private readonly int hubFanOut;

    public NoteWriter(int hubFanOut)
    {
        this.hubFanOut = hubFanOut;
    }

    public List<MapNote> Write(List<ModuleDraft> drafts)
    {
        var notes = new List<MapNote>();
        notes.AddRange(Cycles(drafts));
        notes.AddRange(Hubs(drafts));
        notes.AddRange(Unused(drafts));
        notes.AddRange(Seams(drafts));
        return notes;
    }

    private static IEnumerable<ModuleEdge> Dependencies(ModuleDraft draft)
    {
        return draft.Outgoing.Values.Where(it => it.IsDependency);
    }

    private static string Through(ModuleEdge edge)
    {
        return $"{edge.From.Name} → {edge.To.Name} through {Naming.List(edge.Via.Order(StringComparer.Ordinal), 3)}";
    }

    private static IEnumerable<MapNote> Cycles(List<ModuleDraft> drafts)
    {
        var components = Components.Find(drafts, it => Dependencies(it).Select(edge => edge.To));
        foreach(var component in components.Where(it => it.Count > 1))
        {
            var members = component.ToHashSet();
            var edges = component
                .SelectMany(Dependencies)
                .Where(it => members.Contains(it.To))
                .OrderBy(it => it.From.Name, StringComparer.Ordinal)
                .ThenBy(it => it.To.Name, StringComparer.Ordinal);
            var names = component.Select(it => it.Name).Order(StringComparer.Ordinal).ToList();
            yield return new MapNote()
            {
                Level = "bad",
                Tag = "Cycle",
                Title = names.Count == 2 ? $"{names[0]} and {names[1]}" : $"Cycle of {names.Count} modules",
                Text = "These modules depend on each other: " + string.Join("; ", edges.Select(Through)) + "."
            };
        }
    }

    private IEnumerable<MapNote> Hubs(List<ModuleDraft> drafts)
    {
        foreach(var draft in drafts.Where(it => !it.IsRoot).OrderBy(it => it.Name, StringComparer.Ordinal))
        {
            var targets = Dependencies(draft).Select(it => it.To.Name).Order(StringComparer.Ordinal).ToList();
            if(targets.Count >= hubFanOut)
            {
                yield return new MapNote()
                {
                    Level = "warn",
                    Tag = "Hub",
                    Title = $"{draft.Name} depends on {targets.Count} modules",
                    Text = $"It uses {Naming.List(targets, targets.Count)}. A module this connected is hard to change "
                        + "or test on its own."
                };
            }
        }
    }

    private static IEnumerable<MapNote> Unused(List<ModuleDraft> drafts)
    {
        var unused = drafts
            .Where(it => !it.IsRoot && it.Incoming.Count == 0 && it.Types.Count > 0)
            .Where(it => it.Group.Units.All(unit => !unit.HasMarkup))
            .Select(it => it.Name)
            .Order(StringComparer.Ordinal)
            .ToList();
        if(unused.Count > 0)
        {
            yield return new MapNote()
            {
                Level = "warn",
                Tag = "Unused",
                Title = unused.Count == 1
                    ? $"Nothing references {unused[0]}"
                    : $"{unused.Count} modules are unreferenced",
                Text = $"No other module or composition root refers to {Naming.List(unused, unused.Count)}; "
                    + "it may be dead code or reached only through reflection."
            };
        }
    }

    private static IEnumerable<MapNote> Seams(List<ModuleDraft> drafts)
    {
        var seams = drafts
            .SelectMany(it => it.Outgoing.Values)
            .Where(it => it.Type == "implements" && it.From.Project != it.To.Project)
            .Select(it => $"{it.From.Name} implements {Naming.List(it.Via.Order(StringComparer.Ordinal), 3)} "
                + $"from {it.To.Name}")
            .Order(StringComparer.Ordinal)
            .ToList();
        if(seams.Count > 0)
        {
            yield return new MapNote()
            {
                Level = "ok",
                Tag = "Seam",
                Title = "Interfaces implemented across projects",
                Text = string.Join("; ", seams) + "."
            };
        }
    }
}
