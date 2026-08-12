using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;
using Lore.Core.Logging;

namespace Lore.Core.TextExtractors;

[SupportedExtensions(".odt")]
public class OdtExtractor : ITextExtractor
{
    private readonly ILogger<OdtExtractor> _logger;

    public OdtExtractor(ILogger<OdtExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<string?> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await using var fileStream = File.OpenRead(filePath);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            var contentEntry = archive.GetEntry("content.xml");
            if (contentEntry == null)
            {
                return null;
            }

            await using var entryStream = contentEntry.Open();
            var doc = await XDocument.LoadAsync(entryStream, LoadOptions.None, cancellationToken);

            var sb = new StringBuilder();

            foreach (var element in doc.Descendants())
            {
                if (element.Name.LocalName is "p" or "h")
                {
                    var text = string.Join(" ", element.Value.Trim());
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        sb.AppendLine(text);
                    }
                }
            }

            return sb.ToString();
        }
        catch (Exception ex)
        {
            _logger.ExtractionWarning(filePath, "odt", ex);
            return null;
        }
    }
}