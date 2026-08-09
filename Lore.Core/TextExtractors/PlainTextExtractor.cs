namespace Lore.Core.TextExtractors;

public class PlainTextExtractor : ITextExtractor
{
    public async Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return await File.ReadAllTextAsync(filePath, cancellationToken);
    }
}