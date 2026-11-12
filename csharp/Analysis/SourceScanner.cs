using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ModuleMap.Markup;
using ModuleMap.Repository;

namespace ModuleMap.Analysis;

public sealed class SourceScanner
{
    private static readonly string[] MarkupSuffixes = [".axaml", ".xaml"];
    private readonly GitRepository repository;
    private readonly XamlReader xamlReader;

    public SourceScanner(GitRepository repository, XamlReader xamlReader)
    {
        this.repository = repository;
        this.xamlReader = xamlReader;
    }

    public async Task<SourceIndex> ScanAsync(List<Project> projects)
    {
        var tracked = repository.TrackedFiles();
        var index = new SourceIndex();
        foreach(var project in projects)
        {
            var compilation = await project.GetCompilationAsync();
            if(compilation == null)
            {
                continue;
            }

            string directory = repository.ToRelative(Path.GetDirectoryName(project.FilePath ?? "") ?? "");
            foreach(var document in project.Documents.OrderBy(it => it.FilePath, StringComparer.Ordinal))
            {
                string? path = document.FilePath == null ? null : repository.ToRelative(document.FilePath);
                var tree = await document.GetSyntaxTreeAsync();
                if(path == null || tree == null || !tracked.Contains(path))
                {
                    continue;
                }

                var model = compilation.GetSemanticModel(tree);
                var types = DeclaredTypes(await tree.GetRootAsync(), model);
                index.AddCodeFile(project, path, types, FolderNamespace(project, directory, path));
            }

            foreach(string path in MarkupFiles(tracked, directory))
            {
                var markup = xamlReader.Read(repository.ToAbsolute(path));
                index.AddMarkupFile(project, path, markup, FolderNamespace(project, directory, path));
            }

            var entry = compilation.GetEntryPoint(CancellationToken.None);
            var location = entry?.Locations.FirstOrDefault(it => it.IsInSource);
            if(location?.SourceTree != null)
            {
                index.MarkEntry(repository.ToRelative(location.SourceTree.FilePath));
            }
        }

        return index;
    }

    private static IEnumerable<string> MarkupFiles(HashSet<string> tracked, string directory)
    {
        return tracked
            .Where(it => it.StartsWith(directory + "/", StringComparison.Ordinal))
            .Where(it => MarkupSuffixes.Any(suffix => it.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
            .Order(StringComparer.Ordinal);
    }

    private static List<INamedTypeSymbol> DeclaredTypes(SyntaxNode root, SemanticModel model)
    {
        var types = new List<INamedTypeSymbol>();
        var nodes = root.DescendantNodes(it => it is CompilationUnitSyntax || it is BaseNamespaceDeclarationSyntax);
        foreach(var node in nodes)
        {
            if(node is BaseTypeDeclarationSyntax || node is DelegateDeclarationSyntax)
            {
                if(model.GetDeclaredSymbol(node) is INamedTypeSymbol type)
                {
                    types.Add(type);
                }
            }
        }

        return types;
    }

    private static string FolderNamespace(Project project, string directory, string path)
    {
        string root = project.DefaultNamespace ?? project.Name;
        if(!path.StartsWith(directory + "/", StringComparison.Ordinal))
        {
            return root;
        }

        string folder = Path.GetDirectoryName(path.Substring(directory.Length + 1))?.Replace('\\', '/') ?? "";
        return folder.Length == 0 ? root : root + "." + folder.Replace('/', '.');
    }
}
