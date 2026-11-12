using ModuleMap.Analysis;
using ModuleMap.Grouping;
using ModuleMap.Map;
using ModuleMap.Markup;
using ModuleMap.Repository;
using ModuleMap.Workspace;

namespace ModuleMap;

public static class Program
{
    private const int MaxColumns = 7;
    private const int ProjectGap = 80;
    private const int HubFanOut = 6;
    private const double HiddenShare = 0.5;

    public static async Task<int> Main(string[] args)
    {
        if(args.Length != 2)
        {
            await Console.Error.WriteLineAsync("usage: ModuleMap <solution or project file> <output map.json>");
            return 2;
        }

        string inputPath = Path.GetFullPath(args[0]);
        string outputPath = Path.GetFullPath(args[1]);
        var log = Console.Error;
        var repository = new GitRepository(GitRepository.FindRoot(Path.GetDirectoryName(inputPath) ?? "."));
        var loader = new WorkspaceLoader(log);
        var scanner = new SourceScanner(repository, new XamlReader(log));
        var collector = new ReferenceCollector(repository);
        var packageResolver = new PackageResolver();
        var grouper = new ModuleGrouper();
        var builder = new MapBuilder(
            repository,
            new LayoutEngine(MaxColumns, ProjectGap),
            new ModuleClassifier(),
            new NoteWriter(HubFanOut),
            HiddenShare
        );
        var writer = new MapWriter();

        var solution = await loader.LoadAsync(inputPath);
        var index = await scanner.ScanAsync(solution.Projects);
        await collector.CollectAsync(solution.Projects, index);
        var groups = grouper.Group(index);
        var packages = packageResolver.Resolve(solution.Projects);
        var document = builder.Build(solution, groups, packages, Path.GetFileNameWithoutExtension(inputPath));
        writer.Write(document, outputPath);
        await log.WriteLineAsync($"Wrote {outputPath}: {document.Modules.Count} modules, {document.Edges.Count} edges");
        return 0;
    }
}
