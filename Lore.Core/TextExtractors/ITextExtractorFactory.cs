namespace Lore.Core.TextExtractors;

public interface ITextExtractorFactory
{
    ITextExtractor GetExtractor(string filePath);
}