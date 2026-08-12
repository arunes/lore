namespace Lore.Core.TextExtractors;

[SupportedExtensions(".txt", ".log", ".csv", ".md", ".markdown", ".sql", ".css")]
public class PlainTextExtractor : ITextExtractor
{
    public async Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }
}