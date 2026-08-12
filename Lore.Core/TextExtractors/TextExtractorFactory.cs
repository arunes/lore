using Microsoft.Extensions.DependencyInjection;

namespace Lore.Core.TextExtractors;

public class TextExtractorFactory(IServiceProvider serviceProvider) : ITextExtractorFactory
{
    public ITextExtractor GetExtractor(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("File path cannot be null or empty.", nameof(filePath));
        }

        var extension = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(extension))
        {
            throw new NotSupportedException($"Unable to determine file extension from path '{filePath}'.");
        }

        var key = extension.Trim().ToLowerInvariant();
        return serviceProvider.GetKeyedService<ITextExtractor>(key)
            ?? throw new NotSupportedException($"No extractor available for file extension '{extension}'.");
    }
}
