using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Lore.Core.TextExtractors;

[SupportedExtensions(".odt")]
public class OdtExtractor : ITextExtractor
{
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

            // Extract all text nodes within paragraph/heading nodes
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
        catch (Exception)
        {
            return null;
        }
    }
}
