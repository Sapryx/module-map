using System.Diagnostics;

namespace ModuleMap.Repository;

public sealed class GitRepository
{
    private readonly string root;

    public GitRepository(string root)
    {
        this.root = root;
    }

    public static string FindRoot(string directory)
    {
        return Run(directory, ["rev-parse", "--show-toplevel"]).Trim();
    }

    public HashSet<string> TrackedFiles()
    {
        return Run(root, ["ls-files", "-z"]).Split('\0', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
    }

    public string Commit()
    {
        string hash = Run(root, ["rev-parse", "--short", "HEAD"]).Trim();
        bool dirty = Run(root, ["status", "--porcelain", "--untracked-files=no"]).Trim().Length > 0;
        return dirty ? hash + "+dirty" : hash;
    }

    public string ToRelative(string path)
    {
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }

    public string ToAbsolute(string path)
    {
        return Path.GetFullPath(Path.Combine(root, path));
    }

    private static string Run(string directory, List<string> arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        foreach(string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start) ?? throw new InvalidOperationException("git did not start");
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if(process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {string.Join(' ', arguments)} failed in {directory}");
        }

        return output;
    }
}
