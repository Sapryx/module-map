using Microsoft.CodeAnalysis;

namespace ModuleMap.Workspace;

public sealed class LoadedSolution
{
    public required Solution Solution { get; init; }
    public required List<Project> Projects { get; init; }
    public required List<Project> SkippedProjects { get; init; }
}
