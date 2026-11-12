using Microsoft.CodeAnalysis;
using ModuleMap.Analysis;

namespace ModuleMap.Grouping;

public sealed class ModuleGroup
{
    public required Project Project { get; init; }
    public required string Namespace { get; init; }
    public SourceUnit? ViewModel { get; init; }
    public List<SourceUnit> Units { get; } = [];
}
