using HtmlAgilityPack;
using Lore.Core.TextExtractors;

public class HtmlExtractor : ITextExtractor
{
    public Task<string?> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        var doc = new HtmlDocument();
        doc.Load(filePath);

        // Remove script and style nodes before pulling text
        doc.DocumentNode.Descendants()
            .Where(n => n.Name is "script" or "style")
            .ToList()
            .ForEach(n => n.Remove());

        var text = HtmlEntity.DeEntitize(doc.DocumentNode.InnerText);
        return Task.FromResult<string?>(text);
    }
}
