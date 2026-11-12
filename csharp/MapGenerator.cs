using ModuleMap.Analysis;
using ModuleMap.Grouping;
using ModuleMap.Map;
using ModuleMap.Workspace;

namespace ModuleMap;

public sealed class MapGenerator
{
    private readonly WorkspaceLoader loader;
    private readonly SourceScanner scanner;
    private readonly ReferenceCollector collector;
    private readonly PackageResolver packageResolver;
    private readonly ModuleGrouper grouper;
    private readonly MapBuilder builder;
    private readonly MapRenderer renderer;
    private readonly TextWriter log;

    public MapGenerator(
        WorkspaceLoader loader,
        SourceScanner scanner,
        ReferenceCollector collector,
        PackageResolver packageResolver,
        ModuleGrouper grouper,
        MapBuilder builder,
        MapRenderer renderer,
        TextWriter log
    )
    {
        this.loader = loader;
        this.scanner = scanner;
        this.collector = collector;
        this.packageResolver = packageResolver;
        this.grouper = grouper;
        this.builder = builder;
        this.renderer = renderer;
        this.log = log;
    }

    public async Task<string> GenerateAsync(string solutionPath)
    {
        var solution = await loader.LoadAsync(solutionPath);
        var index = await scanner.ScanAsync(solution.Projects);
        await collector.CollectAsync(solution.Projects, index);
        var groups = grouper.Group(index);
        var packages = packageResolver.Resolve(solution.Projects);
        var document = builder.Build(solution, groups, packages, Path.GetFileNameWithoutExtension(solutionPath));
        await log.WriteLineAsync($"Mapped {document.Modules.Count} modules and {document.Edges.Count} edges");
        return renderer.Render(document);
    }
}
