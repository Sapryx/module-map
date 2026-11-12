using System.Text;

namespace ModuleMap.Map;

public static class Naming
{
    public static string Words(string identifier)
    {
        var builder = new StringBuilder();
        for(int i = 0; i < identifier.Length; i++)
        {
            char current = identifier[i];
            bool startsWord = i > 0 && char.IsUpper(current)
                && (char.IsLower(identifier[i - 1]) || char.IsDigit(identifier[i - 1]));
            if(startsWord)
            {
                builder.Append(' ');
                builder.Append(char.ToLowerInvariant(current));
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }

    public static string Kebab(string text)
    {
        var builder = new StringBuilder();
        foreach(char current in text.ToLowerInvariant())
        {
            builder.Append(char.IsLetterOrDigit(current) ? current : '-');
        }

        string result = builder.ToString();
        while(result.Contains("--"))
        {
            result = result.Replace("--", "-");
        }

        return result.Trim('-');
    }

    public static string List(IEnumerable<string> names, int limit)
    {
        var items = names.ToList();
        string shown = string.Join(", ", items.Take(limit));
        return items.Count > limit ? $"{shown} and {items.Count - limit} more" : shown;
    }
}
