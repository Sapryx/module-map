namespace ModuleMap.Markup;

public sealed class MarkupFile
{
    public string? ClassName { get; init; }
    public string? DataType { get; init; }
    public List<string> ReferencedTypes { get; } = [];
}
