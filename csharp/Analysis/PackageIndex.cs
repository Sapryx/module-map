using Microsoft.CodeAnalysis;

namespace ModuleMap.Analysis;

public sealed class PackageIndex
{
    private readonly Dictionary<string, Dictionary<string, PackageInfo>> packagesByProject;

    public PackageIndex(Dictionary<string, Dictionary<string, PackageInfo>> packagesByProject)
    {
        this.packagesByProject = packagesByProject;
    }

    public PackageInfo? Find(Project project, string assemblyPath)
    {
        var packages = packagesByProject.GetValueOrDefault(project.FilePath ?? "");
        return packages?.GetValueOrDefault(Path.GetFullPath(assemblyPath));
    }
}
