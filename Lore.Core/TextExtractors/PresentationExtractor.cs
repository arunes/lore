using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Lore.Core.TextExtractors;

public partial class PresentationExtractor : ITextExtractor
{
    // Regex to match readable ASCII/Unicode text strings in raw binary streams (.ppt)
    [GeneratedRegex(@"[\x20-\x7E\s]{4,}")]
    private static partial Regex PrintableAsciiRegex();

    public async Task<string?> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".pptx" => await ExtractPptxAsync(filePath, cancellationToken),
            ".ppt" => await ExtractPptFallbackAsync(filePath, cancellationToken),
            _ => null,
        };
    }

    private static async Task<string?> ExtractPptxAsync(
        string filePath,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await using var fileStream = File.OpenRead(filePath);
            using var archive = new ZipArchive(fileStream, ZipArchiveMode.Read);

            // Slides are stored as ppt/slides/slide1.xml, slide2.xml, etc.
            var slideEntries = archive
                .Entries.Where(e =>
                    e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml")
                )
                .OrderBy(e => e.FullName);

            var sb = new StringBuilder();
            int slideNum = 1;

            foreach (var entry in slideEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sb.AppendLine($"--- Slide {slideNum++} ---");

                await using var entryStream = entry.Open();
                var doc = await XDocument.LoadAsync(
                    entryStream,
                    LoadOptions.None,
                    cancellationToken
                );

                // 'a:t' tags in OpenXML contain slide text runs
                var textRuns = doc.Descendants()
                    .Where(e => e.Name.LocalName == "t")
                    .Select(e => e.Value.Trim())
                    .Where(t => !string.IsNullOrEmpty(t));

                foreach (var text in textRuns)
                {
                    sb.AppendLine(text);
                }
            }

            return sb.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<string?> ExtractPptFallbackAsync(
        string filePath,
        CancellationToken cancellationToken
    )
    {
        try
        {
            // Scans the binary file for readable text sequences
            var bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var rawContent = Encoding.Latin1.GetString(bytes);

            var matches = PrintableAsciiRegex().Matches(rawContent);
            var sb = new StringBuilder();

            foreach (Match match in matches)
            {
                var val = match.Value.Trim();
                // Filter out short garbage byte sequences common in OLE headers
                if (val.Length > 3 && !val.StartsWith("OLE") && !val.Contains("Current User"))
                {
                    sb.AppendLine(val);
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
