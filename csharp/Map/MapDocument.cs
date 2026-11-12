namespace ModuleMap.Map;

public sealed class MapDocument
{
    public required string Title { get; init; }
    public required string Lede { get; init; }
    public required string Commit { get; init; }
    public required string Date { get; init; }
    public required MapSource Source { get; init; }
    public required string PackageKind { get; init; }
    public required string NotDrawn { get; init; }
    public required List<MapProject> Projects { get; init; }
    public required List<MapRow> Rows { get; init; }
    public List<string> Shared { get; } = [];
    public required List<MapModule> Modules { get; init; }
    public required List<MapExternal> Externals { get; init; }
    public required List<List<string>> Edges { get; init; }
    public required List<MapNote> Notes { get; init; }
}

public sealed class MapSource
{
    public required string Root { get; init; }
    public required string Profile { get; init; }
    public required List<string> Suffixes { get; init; }
    public required List<string> Exclude { get; init; }
}

public sealed class MapProject
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Sub { get; init; }
}

public sealed class MapRow
{
    public required string Id { get; init; }
    public int? GapBefore { get; init; }
}

public sealed class MapModule
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required string Project { get; init; }
    public required string Ns { get; init; }
    public required double Col { get; init; }
    public required string Row { get; init; }
    public required int Portability { get; init; }
    public required string Iface { get; init; }
    public required string Impl { get; init; }
    public required string Libs { get; init; }
    public required string Note { get; init; }
    public required List<string> Files { get; init; }
}

public sealed class MapExternal
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Sub { get; init; }
    public required string Kind { get; init; }
    public required string Projects { get; init; }
    public required string Note { get; init; }
}

public sealed class MapNote
{
    public required string Level { get; init; }
    public required string Tag { get; init; }
    public required string Title { get; init; }
    public required string Text { get; init; }
}
