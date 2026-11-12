namespace ModuleMap.Preview;

public sealed class LivePreview
{
    private readonly MapGenerator generator;
    private readonly PreviewServer server;
    private readonly SourceWatcher watcher;
    private readonly BrowserLauncher browser;
    private readonly TextWriter log;
    private readonly string url;
    private readonly TimeSpan quietPeriod;

    public LivePreview(
        MapGenerator generator,
        PreviewServer server,
        SourceWatcher watcher,
        BrowserLauncher browser,
        TextWriter log,
        string url,
        TimeSpan quietPeriod
    )
    {
        this.generator = generator;
        this.server = server;
        this.watcher = watcher;
        this.browser = browser;
        this.log = log;
        this.url = url;
        this.quietPeriod = quietPeriod;
    }

    public async Task RunAsync(string solutionPath, CancellationToken token)
    {
        server.Publish(await generator.GenerateAsync(solutionPath));
        var serving = server.ServeAsync(token);
        browser.Open(url);
        await log.WriteLineAsync($"Live preview at {url}, remapping on every source change; press Ctrl+C to stop");
        try
        {
            while(!token.IsCancellationRequested)
            {
                await watcher.WaitForChangesAsync(quietPeriod, token);
                await RemapAsync(solutionPath);
            }
        }
        catch(OperationCanceledException)
        {
        }

        await serving;
    }

    private async Task RemapAsync(string solutionPath)
    {
        await log.WriteLineAsync("Sources changed, remapping");
        try
        {
            server.Publish(await generator.GenerateAsync(solutionPath));
        }
        catch(Exception exception)
        {
            await log.WriteLineAsync($"Remapping failed, keeping the previous map: {exception.Message}");
        }
    }
}
