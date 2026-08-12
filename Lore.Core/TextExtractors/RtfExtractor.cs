using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Lore.Core.Logging;

namespace Lore.Core.TextExtractors;

[SupportedExtensions(".rtf")]
public partial class RtfExtractor : ITextExtractor
{
    private readonly ILogger<RtfExtractor> _logger;

    public RtfExtractor(ILogger<RtfExtractor> logger)
    {
        _logger = logger;
    }

    [GeneratedRegex(@"\{\*?\\[^{}]+}|[{}]|\\\w+\b ?")]
    private static partial Regex RtfControlRegex();

    public async Task<string?> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var rtfContent = await File.ReadAllTextAsync(filePath, cancellationToken);
            if (string.IsNullOrWhiteSpace(rtfContent))
            {
                return null;
            }

            var plainText = RtfControlRegex().Replace(rtfContent, string.Empty);
            return plainText.Trim();
        }
        catch (Exception ex)
        {
            _logger.ExtractionWarning(filePath, "rtf", ex);
            return null;
        }
    }
}