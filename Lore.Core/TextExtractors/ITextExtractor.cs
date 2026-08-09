namespace Lore.Core.TextExtractors;

public interface ITextExtractor
{
    Task<string?> ExtractTextAsync(string filePath, CancellationToken cancellationToken = default);
}