namespace ModuleMap.Preview;

public sealed class SourceWatcher
{
    private static readonly string[] Suffixes =
    [
        ".cs",
        ".axaml",
        ".xaml",
        ".csproj",
        ".props",
        ".targets",
        ".sln",
        ".slnx"
    ];
    private static readonly HashSet<string> IgnoredFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bin",
        "obj",
        ".git",
        ".idea",
        ".vs"
    };
    private readonly SemaphoreSlim changes = new SemaphoreSlim(0);

    public SourceWatcher(FileSystemWatcher watcher)
    {
        watcher.Changed += (_, change) => OnChange(change.FullPath);
        watcher.Created += (_, change) => OnChange(change.FullPath);
        watcher.Deleted += (_, change) => OnChange(change.FullPath);
        watcher.Renamed += (_, change) =>
        {
            OnChange(change.OldFullPath);
            OnChange(change.FullPath);
        };
        watcher.EnableRaisingEvents = true;
    }

    public async Task WaitForChangesAsync(TimeSpan quietPeriod, CancellationToken token)
    {
        await changes.WaitAsync(token);
        while(await changes.WaitAsync(quietPeriod, token))
        {
        }
    }

    private void OnChange(string path)
    {
        bool isSource = Suffixes.Any(it => path.EndsWith(it, StringComparison.OrdinalIgnoreCase));
        bool isIgnored = path.Split(Path.DirectorySeparatorChar, '/').Any(IgnoredFolders.Contains);
        if(isSource && !isIgnored)
        {
            changes.Release();
        }
    }
}
