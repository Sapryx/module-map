using Microsoft.CodeAnalysis;
using ModuleMap.Markup;

namespace ModuleMap.Analysis;

public sealed class SourceIndex
{
    public List<SourceUnit> Units { get; } = [];
    private readonly Dictionary<string, SourceUnit> unitsByFile = new Dictionary<string, SourceUnit>();
    private readonly Dictionary<string, SourceUnit> unitsByType = new Dictionary<string, SourceUnit>();

    public static string FullName(INamedTypeSymbol type)
    {
        string ns = NamespaceOf(type);
        return ns.Length == 0 ? type.MetadataName : ns + "." + type.MetadataName;
    }

    public static string NamespaceOf(ISymbol symbol)
    {
        if(symbol.ContainingNamespace == null || symbol.ContainingNamespace.IsGlobalNamespace)
        {
            return "";
        }

        return symbol.ContainingNamespace.ToDisplayString();
    }

    public SourceUnit? FindByFile(string path)
    {
        return unitsByFile.GetValueOrDefault(path);
    }

    public SourceUnit? FindByType(string fullName)
    {
        return unitsByType.GetValueOrDefault(fullName);
    }

    public void AddCodeFile(Project project, string path, List<INamedTypeSymbol> types, string folderNamespace)
    {
        var owners = types.Select(it => FindByType(FullName(it))).OfType<SourceUnit>().Distinct().ToList();
        SourceUnit unit;
        if(owners.Count == 0)
        {
            var primary = types.FirstOrDefault();
            unit = primary == null
                ? Create(project, Path.GetFileNameWithoutExtension(path), folderNamespace)
                : Create(project, primary.Name, NamespaceOf(primary));
        }
        else
        {
            unit = owners[0];
            foreach(var other in owners.Skip(1))
            {
                Merge(unit, other);
            }
        }

        AddFile(unit, path);
        foreach(var type in types)
        {
            string name = FullName(type);
            unit.Types[name] = type;
            unitsByType[name] = unit;
        }
    }

    public void AddMarkupFile(Project project, string path, MarkupFile markup, string folderNamespace)
    {
        var unit = markup.ClassName == null ? null : FindByType(markup.ClassName);
        if(unit == null)
        {
            unit = Create(project, Path.GetFileNameWithoutExtension(path), folderNamespace);
        }

        AddFile(unit, path);
        if(markup.DataType != null)
        {
            unit.RootDataTypes.Add(markup.DataType);
        }

        unit.MarkupReferences.AddRange(markup.ReferencedTypes);
    }

    public void MarkEntry(string path)
    {
        var unit = FindByFile(path);
        if(unit != null)
        {
            unit.IsEntry = true;
        }
    }

    private SourceUnit Create(Project project, string name, string ns)
    {
        var unit = new SourceUnit() { Name = name, Namespace = ns, Project = project };
        Units.Add(unit);
        return unit;
    }

    private void AddFile(SourceUnit unit, string path)
    {
        unit.Files.Add(path);
        unitsByFile[path] = unit;
    }

    private void Merge(SourceUnit target, SourceUnit source)
    {
        foreach(string path in source.Files)
        {
            AddFile(target, path);
        }

        foreach(var pair in source.Types)
        {
            target.Types[pair.Key] = pair.Value;
            unitsByType[pair.Key] = target;
        }

        target.RootDataTypes.AddRange(source.RootDataTypes);
        target.MarkupReferences.AddRange(source.MarkupReferences);
        Units.Remove(source);
    }
}
