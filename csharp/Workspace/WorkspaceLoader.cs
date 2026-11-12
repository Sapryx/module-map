using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace ModuleMap.Workspace;

public sealed class WorkspaceLoader
{
    private static readonly string[] TestAssemblyPrefixes =
    [
        "xunit",
        "nunit",
        "MSTest",
        "Microsoft.VisualStudio.TestPlatform",
        "Microsoft.TestPlatform"
    ];
    private readonly TextWriter log;

    public WorkspaceLoader(TextWriter log)
    {
        this.log = log;
    }

    public async Task<LoadedSolution> LoadAsync(string path)
    {
        var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(it => log.WriteLine($"workspace: {it.Diagnostic.Message}"));
        var solution = path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? (await workspace.OpenProjectAsync(path)).Solution
            : await workspace.OpenSolutionAsync(path);
        var projects = new List<Project>();
        var skipped = new List<Project>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var project in solution.Projects.OrderBy(it => it.Name, StringComparer.Ordinal))
        {
            if(project.FilePath == null || !seen.Add(project.FilePath))
            {
                continue;
            }

            if(IsTestProject(project))
            {
                skipped.Add(project);
            }
            else
            {
                projects.Add(project);
            }
        }

        return new LoadedSolution() { Solution = solution, Projects = projects, SkippedProjects = skipped };
    }

    private static bool IsTestProject(Project project)
    {
        if(project.AssemblyName.Split('.').Any(it => it == "Tests" || it == "Test"))
        {
            return true;
        }

        return project.MetadataReferences
            .OfType<PortableExecutableReference>()
            .Select(it => Path.GetFileName(it.FilePath ?? ""))
            .Any(name => TestAssemblyPrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)));
    }
}
