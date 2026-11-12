using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ModuleMap.Map;

public sealed class MapRenderer
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly string template;

    public MapRenderer(string template)
    {
        this.template = template;
    }

    public string Render(MapDocument document)
    {
        string data = JsonSerializer.Serialize(document, Options).Replace("</", "<\\/");
        return template
            .Replace("__MAP_TITLE__", WebUtility.HtmlEncode(document.Title))
            .Replace("__MAP_DATA__", data);
    }
}
