using System.Text.Json;
using Microsoft.CodeAnalysis;

namespace ModuleMap.Analysis;

public sealed class PackageResolver
{
    private static readonly string[] AssemblySections = ["compile", "runtime"];

    public PackageIndex Resolve(List<Project> projects)
    {
        var packagesByProject = new Dictionary<string, Dictionary<string, PackageInfo>>();
        foreach(var project in projects)
        {
            string directory = Path.GetDirectoryName(project.FilePath ?? "") ?? "";
            string assetsPath = Path.Combine(directory, "obj", "project.assets.json");
            var packages = new Dictionary<string, PackageInfo>(StringComparer.OrdinalIgnoreCase);
            if(File.Exists(assetsPath))
            {
                AddAssets(assetsPath, packages);
            }

            packagesByProject[project.FilePath ?? ""] = packages;
        }

        return new PackageIndex(packagesByProject);
    }

    private static void AddAssets(string assetsPath, Dictionary<string, PackageInfo> packages)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(assetsPath));
        var root = document.RootElement;
        var folders = root.GetProperty("packageFolders").EnumerateObject().Select(it => it.Name).ToList();
        var libraries = root.GetProperty("libraries");
        var target = root.GetProperty("targets").EnumerateObject().First(it => !it.Name.Contains('/')).Value;
        var entries = new Dictionary<string, JsonProperty>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in target.EnumerateObject())
        {
            bool isPackage = !entry.Value.TryGetProperty("type", out var type) || type.GetString() == "package";
            if(isPackage)
            {
                entries[entry.Name.Substring(0, entry.Name.IndexOf('/'))] = entry;
            }
        }

        foreach(var pair in Owners(DirectDependencies(root), entries))
        {
            if(!entries.TryGetValue(pair.Key, out var entry) || !entries.TryGetValue(pair.Value, out var owner))
            {
                continue;
            }

            var info = new PackageInfo()
            {
                Id = owner.Name.Substring(0, owner.Name.IndexOf('/')),
                Version = owner.Name.Substring(owner.Name.IndexOf('/') + 1)
            };
            string libraryPath = libraries.GetProperty(entry.Name).GetProperty("path").GetString() ?? "";
            foreach(string file in AssemblyFiles(entry.Value))
            {
                foreach(string folder in folders)
                {
                    packages[Path.GetFullPath(Path.Combine(folder, libraryPath, file))] = info;
                }
            }
        }
    }

    private static List<string> DirectDependencies(JsonElement root)
    {
        var frameworks = root.GetProperty("project").GetProperty("frameworks").EnumerateObject().ToList();
        if(frameworks.Count == 0 || !frameworks[0].Value.TryGetProperty("dependencies", out var dependencies))
        {
            return [];
        }

        return dependencies.EnumerateObject().Select(it => it.Name).Order(StringComparer.Ordinal).ToList();
    }

    private static Dictionary<string, string> Owners(List<string> direct, Dictionary<string, JsonProperty> entries)
    {
        var distances = direct.ToDictionary(it => it, it => Distances(it, entries));
        var owners = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach(string id in distances.Values.SelectMany(it => it.Keys).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidates = direct.Where(it => distances[it].ContainsKey(id)).ToList();
            string? named = candidates
                .Where(it => id.Equals(it, StringComparison.OrdinalIgnoreCase)
                    || id.StartsWith(it + ".", StringComparison.OrdinalIgnoreCase))
                .MaxBy(it => it.Length);
            owners[id] = named ?? candidates
                .OrderBy(it => distances[it][id])
                .ThenBy(it => it, StringComparer.Ordinal)
                .First();
        }

        return owners;
    }

    private static Dictionary<string, int> Distances(string start, Dictionary<string, JsonProperty> entries)
    {
        var distances = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [start] = 0 };
        var queue = new Queue<string>([start]);
        while(queue.Count > 0)
        {
            string current = queue.Dequeue();
            if(!entries.TryGetValue(current, out var entry)
                || !entry.Value.TryGetProperty("dependencies", out var dependencies))
            {
                continue;
            }

            foreach(var dependency in dependencies.EnumerateObject())
            {
                if(distances.TryAdd(dependency.Name, distances[current] + 1))
                {
                    queue.Enqueue(dependency.Name);
                }
            }
        }

        return distances;
    }

    private static List<string> AssemblyFiles(JsonElement entry)
    {
        var files = new List<string>();
        foreach(string section in AssemblySections)
        {
            if(entry.TryGetProperty(section, out var items))
            {
                files.AddRange(items.EnumerateObject()
                    .Select(it => it.Name)
                    .Where(it => it.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)));
            }
        }

        return files;
    }
}
