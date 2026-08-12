using System.Text;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Lore.Core.Logging;

namespace Lore.Core.TextExtractors;

[SupportedExtensions(
    ".xml", ".xhtml", ".xht", ".gpx",
    ".kml", ".svg", ".rss", ".atom",
    ".plist", ".xlf", ".xliff", ".wsdl",
    ".xslt", ".xsl", ".xsd", ".config",
    ".csproj", ".vbproj")]
public class XmlExtractor : ITextExtractor
{
    private readonly ILogger<XmlExtractor> _logger;

    public XmlExtractor(ILogger<XmlExtractor> logger)
    {
        _logger = logger;
    }

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
        catch (XmlException ex)
        {
            _logger.ExtractionWarning(filePath, "xml", ex);
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

        var elementName = element.Name.LocalName;
        var path = string.IsNullOrEmpty(currentPath) ? elementName : $"{currentPath}.{elementName}";

        foreach (var attr in element.Attributes())
        {
            if (!string.IsNullOrWhiteSpace(attr.Value))
            {
                sb.AppendLine($"{path}.{attr.Name.LocalName}: {attr.Value.Trim()}");
            }
        }

        var textNodes = element
            .Nodes()
            .OfType<XText>()
            .Select(t => t.Value.Trim())
            .Where(v => !string.IsNullOrEmpty(v))
            .ToList();

        var childElements = element.Elements().ToList();

        if (textNodes.Count > 0 && childElements.Count == 0)
        {
            var textValue = string.Join(" ", textNodes);
            sb.AppendLine($"{path}: {textValue}");
        }

        foreach (var child in childElements)
        {
            FlattenElement(child, path, sb, cancellationToken);
        }
    }
}