using System.Text.RegularExpressions;

namespace Lore.Core.TextExtractors;

[SupportedExtensions(".rtf")]
public partial class RtfExtractor : ITextExtractor
{
    // Regex to remove RTF formatting controls and groups
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

            // Strip RTF control sequences
            var plainText = RtfControlRegex().Replace(rtfContent, string.Empty);
            return plainText.Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
