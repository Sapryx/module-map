using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace ModuleMap.Workspace;

public sealed partial class SolutionReader
{
    public List<string> ProjectPaths(string path)
    {
        if(path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return [path];
        }

        string directory = Path.GetDirectoryName(path) ?? ".";
        var relative = path.EndsWith(".slnx", StringComparison.OrdinalIgnoreCase)
            ? XDocument.Load(path).Descendants("Project").Select(it => it.Attribute("Path")?.Value).OfType<string>()
            : SlnProject().Matches(File.ReadAllText(path)).Select(it => it.Groups[1].Value);
        return relative
            .Where(it => it.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .Select(it => Path.GetFullPath(Path.Combine(directory, it.Replace('\\', Path.DirectorySeparatorChar))))
            .ToList();
    }

    [GeneratedRegex(@"^Project\(""[^""]*""\)\s*=\s*""[^""]*"",\s*""([^""]+)""", RegexOptions.Multiline)]
    private static partial Regex SlnProject();
}
