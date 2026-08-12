using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Lore.Core.TextExtractors;

[SupportedExtensions(
    ".xml", ".xhtml", ".xht", ".gpx",
    ".kml", ".svg", ".rss", ".atom",
    ".plist", ".xlf", ".xliff", ".wsdl",
    ".xslt", ".xsl", ".xsd", ".config",
    ".csproj", ".vbproj")]
public class XmlExtractor : ITextExtractor
{
    public Task<string?> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var stream = File.OpenRead(filePath);
            var doc = XDocument.Load(stream);

            if (doc.Root == null)
            {
                return Task.FromResult<string?>(string.Empty);
            }

            var sb = new StringBuilder();
            FlattenElement(doc.Root, string.Empty, sb, cancellationToken);

            return Task.FromResult<string?>(sb.ToString());
        }
        catch (XmlException)
        {
            // Invalid XML structure
            return Task.FromResult<string?>(null);
        }
    }

    private static void FlattenElement(
        XElement element,
        string currentPath,
        StringBuilder sb,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Build path: root.child.subchild
        var elementName = element.Name.LocalName;
        var path = string.IsNullOrEmpty(currentPath) ? elementName : $"{currentPath}.{elementName}";

        // 1. Process Attributes (e.g. <product id="101"> -> product.id: 101)
        foreach (var attr in element.Attributes())
        {
            if (!string.IsNullOrWhiteSpace(attr.Value))
            {
                sb.AppendLine($"{path}.{attr.Name.LocalName}: {attr.Value.Trim()}");
            }
        }

        // 2. Check if this node has direct text content (Leaf Node)
        var textNodes = element
            .Nodes()
            .OfType<XText>()
            .Select(t => t.Value.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

        var childElements = element.Elements().ToList();

        if (textNodes.Count > 0 && childElements.Count == 0)
        {
            // Leaf element with value
            var textValue = string.Join(" ", textNodes);
            sb.AppendLine($"{path}: {textValue}");
        }

        // 3. Recurse into child elements
        foreach (var child in childElements)
        {
            FlattenElement(child, path, sb, cancellationToken);
        }
    }
}
