namespace Lore.Core.RAG;

internal static class RAGTextExtensions
{
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