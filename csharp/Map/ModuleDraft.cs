using Microsoft.CodeAnalysis;
using ModuleMap.Analysis;
using ModuleMap.Grouping;

namespace ModuleMap.Map;

public sealed class ModuleDraft
{
    public required ModuleGroup Group { get; init; }
    public required string Id { get; init; }
    public required string Name { get; init; }
    public Dictionary<ModuleDraft, ModuleEdge> Outgoing { get; } = new Dictionary<ModuleDraft, ModuleEdge>();
    public List<ModuleEdge> Incoming { get; } = [];
    public Dictionary<string, PackageInfo> Packages { get; } = new Dictionary<string, PackageInfo>();
    public HashSet<string> Natives { get; } = [];
    public bool IsRoot { get; set; }
    public bool IsHidden { get; set; }
    public string Kind { get; set; } = "service";
    public int Portability { get; set; } = 2;
    public double Col { get; set; }
    public string Row { get; set; } = "";
    public string Project => Group.Project.Name;
    public List<INamedTypeSymbol> Types => Group.Units.SelectMany(it => it.Types.Values).ToList();
}
