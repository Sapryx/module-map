using System.Xml;
using System.Xml.Linq;

namespace ModuleMap.Markup;

public sealed class XamlReader
{
    private static readonly XNamespace XamlNamespace = "http://schemas.microsoft.com/winfx/2006/xaml";
    private readonly TextWriter log;

    public XamlReader(TextWriter log)
    {
        this.log = log;
    }

    public MarkupFile Read(string path)
    {
        XDocument document;
        try
        {
            document = XDocument.Load(path);
        }
        catch(XmlException exception)
        {
            log.WriteLine($"markup: skipped {path}: {exception.Message}");
            return new MarkupFile();
        }

        if(document.Root == null)
        {
            return new MarkupFile();
        }

        var root = document.Root;
        var file = new MarkupFile()
        {
            ClassName = root.Attribute(XamlNamespace + "Class")?.Value,
            DataType = Resolve(root, root.Attribute(XamlNamespace + "DataType")?.Value)
        };
        foreach(var element in root.DescendantsAndSelf())
        {
            string? clrNamespace = ClrNamespace(element.Name.NamespaceName);
            if(clrNamespace != null)
            {
                file.ReferencedTypes.Add(clrNamespace + "." + element.Name.LocalName.Split('.')[0]);
            }

            string? dataType = Resolve(element, element.Attribute(XamlNamespace + "DataType")?.Value);
            if(dataType != null)
            {
                file.ReferencedTypes.Add(dataType);
            }
        }

        return file;
    }

    private static string? Resolve(XElement element, string? value)
    {
        if(value == null || value.StartsWith('{'))
        {
            return null;
        }

        int colon = value.IndexOf(':');
        var ns = colon < 0 ? element.GetDefaultNamespace() : element.GetNamespaceOfPrefix(value.Substring(0, colon));
        string? clrNamespace = ns == null ? null : ClrNamespace(ns.NamespaceName);
        return clrNamespace == null ? null : clrNamespace + "." + value.Substring(colon + 1);
    }

    private static string? ClrNamespace(string uri)
    {
        if(uri.StartsWith("using:", StringComparison.Ordinal))
        {
            return uri.Substring("using:".Length);
        }

        if(uri.StartsWith("clr-namespace:", StringComparison.Ordinal))
        {
            return uri.Substring("clr-namespace:".Length).Split(';')[0];
        }

        return null;
    }
}
