using System.Net;
using System.Net.Sockets;
using ModuleMap.Analysis;
using ModuleMap.Grouping;
using ModuleMap.Map;
using ModuleMap.Markup;
using ModuleMap.Preview;
using ModuleMap.Repository;
using ModuleMap.Workspace;

namespace ModuleMap;

public static class Program
{
    private const int MaxColumns = 7;
    private const int ProjectGap = 80;
    private const int HubFanOut = 6;
    private const double HiddenShare = 0.5;
    private const string OutOption = "--out";
    private static readonly TimeSpan QuietPeriod = TimeSpan.FromMilliseconds(800);

    public static async Task<int> Main(string[] args)
    {
        bool valid = args.Length == 1 || args.Length == 3 && args[1] == OutOption;
        if(!valid)
        {
            await Console.Error.WriteLineAsync(
                $"usage: ModuleMap <solution or project file> [{OutOption} <page.html>]"
            );
            return 2;
        }

        string inputPath = Path.GetFullPath(args[0]);
        string? outputPath = args.Length == 3 ? Path.GetFullPath(args[2]) : null;
        var log = Console.Error;
        string root = GitRepository.FindRoot(Path.GetDirectoryName(inputPath) ?? ".");
        var repository = new GitRepository(root);
        string template = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "template.html"));
        var generator = new MapGenerator(
            new WorkspaceLoader(new SolutionReader(), log),
            new SourceScanner(repository, new XamlReader(log)),
            new ReferenceCollector(repository),
            new PackageResolver(),
            new ModuleGrouper(),
            new MapBuilder(
                repository,
                new LayoutEngine(MaxColumns, ProjectGap),
                new ModuleClassifier(),
                new NoteWriter(HubFanOut),
                HiddenShare
            ),
            new MapRenderer(template),
            log
        );

        if(outputPath != null)
        {
            await File.WriteAllTextAsync(outputPath, await generator.GenerateAsync(inputPath));
            await log.WriteLineAsync($"Wrote {outputPath}");
            return 0;
        }

        string url = $"http://localhost:{FreePort()}/";
        var listener = new HttpListener();
        listener.Prefixes.Add(url);
        using var fileWatcher = new FileSystemWatcher(root)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite
        };
        using var stop = new CancellationTokenSource();
        Console.CancelKeyPress += (_, press) =>
        {
            press.Cancel = true;
            stop.Cancel();
        };
        var preview = new LivePreview(
            generator,
            new PreviewServer(listener),
            new SourceWatcher(fileWatcher),
            new BrowserLauncher(log),
            log,
            url,
            QuietPeriod
        );
        await preview.RunAsync(inputPath, stop.Token);
        listener.Close();
        return 0;
    }

    private static int FreePort()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }
}
