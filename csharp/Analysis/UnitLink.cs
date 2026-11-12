namespace ModuleMap.Analysis;

public sealed class UnitLink
{
    public HashSet<string> TypeNames { get; } = [];
    public bool Implements { get; set; }
    public bool Listens { get; set; }
}
