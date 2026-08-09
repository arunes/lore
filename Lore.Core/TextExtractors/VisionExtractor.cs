namespace Lore.Core.TextExtractors;

public class VisionExtractor : ITextExtractor
{
    public Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // TODO: OCR
        return Task.FromResult<string?>(null);
    }
}