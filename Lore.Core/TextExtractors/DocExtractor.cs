using Microsoft.Extensions.Logging;
using Lore.Core.Logging;
using NPOI.HWPF;
using NPOI.HWPF.Extractor;

namespace Lore.Core.TextExtractors;

[SupportedExtensions(".doc")]
public class DocExtractor : ITextExtractor
{
    private readonly ILogger<DocExtractor> _logger;

    public DocExtractor(ILogger<DocExtractor> logger)
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
            await using var stream = File.OpenRead(filePath);
            var document = new HWPFDocument(stream);
            var extractor = new WordExtractor(document);
            var extractedText = extractor.Text;

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return null;
            }

            var cleanText = string.Join(
                Environment.NewLine,
                extractedText
                    .Replace("\r\n", "\n")
                    .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
            );

            return string.IsNullOrWhiteSpace(cleanText) ? null : cleanText;
        }
        catch (Exception ex)
        {
            _logger.ExtractionWarning(filePath, "doc", ex);
            return null;
        }
    }
}