using Microsoft.CodeAnalysis;

namespace ModuleMap.Analysis;

public sealed class SourceUnit
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required Project Project { get; init; }
    public List<string> Files { get; } = [];
    public Dictionary<string, INamedTypeSymbol> Types { get; } = new Dictionary<string, INamedTypeSymbol>();
    public List<string> RootDataTypes { get; } = [];
    public List<string> MarkupReferences { get; } = [];
    public Dictionary<SourceUnit, UnitLink> Links { get; } = new Dictionary<SourceUnit, UnitLink>();
    public HashSet<string> ExternalAssemblies { get; } = [];
    public HashSet<string> NativeLibraries { get; } = [];
    public bool IsEntry { get; set; }
    public bool HasMarkup => Files.Any(it => it.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)
        || it.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase));

    public UnitLink LinkTo(SourceUnit target)
    {
        if(!Links.TryGetValue(target, out var link))
        {
            link = new UnitLink();
            Links[target] = link;
        }

        return link;
    }
}
