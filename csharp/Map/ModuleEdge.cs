namespace ModuleMap.Map;

public sealed class ModuleEdge
{
    public required ModuleDraft From { get; init; }
    public required ModuleDraft To { get; init; }
    public HashSet<string> Via { get; } = [];
    public bool Implements { get; set; }
    public bool Listens { get; set; }
    public string Type { get; set; } = "uses";
    public bool IsVisible => !To.IsHidden;
    public bool IsDependency => Type != "wiring" && IsVisible;
}
