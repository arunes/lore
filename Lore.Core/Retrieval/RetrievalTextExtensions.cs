using System.Text.RegularExpressions;

namespace Lore.Core.Retrieval;

internal static partial class RetrievalTextExtensions
{
    // Compile-time generated regexes
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraNewlinesRegex();

    [GeneratedRegex(@"[ \t]+\n")]
    private static partial Regex TrailingSpacesRegex();

    public static string? CleanTextForRAG(this string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var text = new string([
            .. input.Where(c => !char.IsControl(c) || c is '\r' or '\n' or '\t'),
        ]);

        text = text.Replace('\u00A0', ' ') // Non-breaking space
            .Replace('\u200B', ' ') // Zero-width space
            .Replace("\r\n", "\n")
            .Replace('\r', '\n');

        text = TrailingSpacesRegex().Replace(text, "\n");
        text = ExtraNewlinesRegex().Replace(text, "\n\n");
        text = text.Trim();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }
}