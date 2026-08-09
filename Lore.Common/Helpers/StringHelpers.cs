namespace Lore.Common.Helpers;

using System.Text.RegularExpressions;

public static partial class StringHelpers
{
    // Compile-time generated regexes
    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExtraNewlinesRegex();

    [GeneratedRegex(@"[ \t]+\n")]
    private static partial Regex TrailingSpacesRegex();

    [GeneratedRegex(@"[^\w\s""]", RegexOptions.Compiled)]
    private static partial Regex NoWordAndWhitespaceRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?<match>""[^""]+"")|(?<match>\S+)")]
    private static partial Regex SpaceMatchesRegex();

    public static string? CleanTextForRAG(string? input)
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

    public static string CleanLLMJsonOutput(string output)
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

    public static string FormatFtsKeywords(string rawKeywords)
    {
        if (string.IsNullOrWhiteSpace(rawKeywords))
        {
            return string.Empty;
        }

        // 1. Sanitize special characters
        string cleaned = NoWordAndWhitespaceRegex().Replace(rawKeywords, " ").Trim();

        // 2. Tokenize by space, keeping quoted phrases intact
        var matches = SpaceMatchesRegex()
            .Matches(cleaned)
            .Select(m => m.Groups["match"].Value)
            .Where(term => !string.IsNullOrWhiteSpace(term));

        // 3. Join with OR for broad recall (BM25 ORDER BY rank handles precision)
        return string.Join(" OR ", matches);
    }
}
