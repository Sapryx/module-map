namespace ModuleMap.Map;

public sealed class LayoutEngine
{
    private readonly int maxColumns;
    private readonly int projectGap;

    public LayoutEngine(int maxColumns, int projectGap)
    {
        this.maxColumns = maxColumns;
        this.projectGap = projectGap;
    }

    public List<MapRow> Arrange(List<ModuleDraft> drafts, List<string> projects)
    {
        var bands = projects
            .Select(project => Rows(drafts.Where(it => it.Project == project).ToList()))
            .ToList();
        int width = bands.SelectMany(it => it).Max(it => it.Count);
        var positions = new Dictionary<ModuleDraft, double>();
        var rows = new List<MapRow>();
        for(int band = 0; band < bands.Count; band++)
        {
            for(int row = 0; row < bands[band].Count; row++)
            {
                var ordered = bands[band][row]
                    .OrderBy(it => Barycenter(it, positions) ?? double.MaxValue)
                    .ThenBy(it => it.Name, StringComparer.Ordinal)
                    .ToList();
                double offset = (width - ordered.Count) / 2.0;
                var mapRow = new MapRow()
                {
                    Id = "r" + (rows.Count + 1),
                    GapBefore = row == 0 && band > 0 ? projectGap : null
                };
                rows.Add(mapRow);
                for(int i = 0; i < ordered.Count; i++)
                {
                    ordered[i].Col = offset + i;
                    ordered[i].Row = mapRow.Id;
                    positions[ordered[i]] = offset + i;
                }
            }
        }

        return rows;
    }

    private static IEnumerable<ModuleDraft> Neighbors(ModuleDraft draft)
    {
        return draft.Outgoing.Values.Where(it => it.IsVisible).Select(it => it.To)
            .Concat(draft.Incoming.Where(it => it.IsVisible).Select(it => it.From));
    }

    private static double? Barycenter(ModuleDraft draft, Dictionary<ModuleDraft, double> positions)
    {
        var placed = Neighbors(draft).Where(positions.ContainsKey).Select(it => positions[it]).ToList();
        return placed.Count == 0 ? null : placed.Average();
    }

    private List<List<ModuleDraft>> Rows(List<ModuleDraft> members)
    {
        var levels = Levels(members);
        return members
            .GroupBy(it => levels[it])
            .OrderByDescending(it => it.Key)
            .SelectMany(level => level.OrderBy(it => it.Name, StringComparer.Ordinal).Chunk(maxColumns))
            .Select(it => it.ToList())
            .ToList();
    }

    private static Dictionary<ModuleDraft, int> Levels(List<ModuleDraft> members)
    {
        var inside = members.ToHashSet();
        var components = Components.Find(members, it => Next(it, inside));
        var componentOf = new Dictionary<ModuleDraft, List<ModuleDraft>>();
        foreach(var component in components)
        {
            foreach(var member in component)
            {
                componentOf[member] = component;
            }
        }

        var componentLevels = new Dictionary<List<ModuleDraft>, int>();
        foreach(var component in components)
        {
            int level = component
                .SelectMany(it => Next(it, inside))
                .Select(it => componentOf[it])
                .Where(it => it != component)
                .Select(it => componentLevels[it] + 1)
                .DefaultIfEmpty(0)
                .Max();
            componentLevels[component] = level;
        }

        return members.ToDictionary(it => it, it => componentLevels[componentOf[it]]);
    }

    private static IEnumerable<ModuleDraft> Next(ModuleDraft draft, HashSet<ModuleDraft> inside)
    {
        return draft.Outgoing.Values.Where(it => it.IsVisible && inside.Contains(it.To)).Select(it => it.To);
    }
}
