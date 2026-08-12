using Lore.Core.TextExtractors;

[SupportedExtensions(".pem", ".ppk", ".zip", ".vsd", ".cdr", ".ai", ".eps", ".mp4")]
public class NoOpExtractor : ITextExtractor
{
    public Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<string?>(null);
    }
}