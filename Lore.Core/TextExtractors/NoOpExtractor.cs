using Lore.Core.TextExtractors;

public class NoOpExtractor : ITextExtractor
{
    public Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}