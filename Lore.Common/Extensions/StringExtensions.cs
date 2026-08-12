using System.Text.RegularExpressions;

namespace Lore.Common.Extensions;

public static partial class StringExtensions
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

    public static string CleanLLMJsonOutput(this string output)
    {
        var cleanText = output.Trim();
        if (cleanText.StartsWith("```json"))
        {
            cleanText = cleanText[7..];
        }
        if (cleanText.StartsWith("```"))
        {
            cleanText = cleanText[3..];
        }
        if (cleanText.EndsWith("```"))
        {
            cleanText = cleanText[..^3];
        }

        return cleanText.Replace("\u00A0", " ").Replace("&nbsp;", " ");
    }
}