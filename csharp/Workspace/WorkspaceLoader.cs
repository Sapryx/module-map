using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace ModuleMap.Workspace;

public sealed class WorkspaceLoader
{
    private static readonly HashSet<string> TestNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Test",
        "Tests"
    };
    private readonly SolutionReader solutionReader;
    private readonly TextWriter log;

    public WorkspaceLoader(SolutionReader solutionReader, TextWriter log)
    {
        this.solutionReader = solutionReader;
        this.log = log;
    }

    public async Task<LoadedSolution> LoadAsync(string path)
    {
        string root = Path.GetDirectoryName(path) ?? ".";
        var paths = solutionReader.ProjectPaths(path);
        var skipped = paths.Where(it => IsTestProject(it, root)).ToList();
        var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(it => log.WriteLine($"workspace: {it.Diagnostic.Message}"));
        foreach(string projectPath in paths.Except(skipped))
        {
            bool loaded = workspace.CurrentSolution.Projects
                .Any(it => string.Equals(it.FilePath, projectPath, StringComparison.OrdinalIgnoreCase));
            if(!loaded)
            {
                await workspace.OpenProjectAsync(projectPath);
            }
        }

        var projects = new List<Project>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var project in workspace.CurrentSolution.Projects.OrderBy(it => it.Name, StringComparer.Ordinal))
        {
            if(project.FilePath != null && !IsTestProject(project.FilePath, root) && seen.Add(project.FilePath))
            {
                projects.Add(project);
            }
        }

        return new LoadedSolution()
        {
            Solution = workspace.CurrentSolution,
            Projects = projects
        };
    }

    private static bool IsTestProject(string projectPath, string root)
    {
        var nameParts = Path.GetFileNameWithoutExtension(projectPath).Split('.');
        string directory = Path.GetRelativePath(root, Path.GetDirectoryName(projectPath) ?? root);
        var folders = directory.Split(Path.DirectorySeparatorChar, '/');
        return nameParts.Any(TestNames.Contains) || folders.Any(TestNames.Contains);
    }
}
