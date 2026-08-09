using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace Lore.Core.TextExtractors;

public class PdfExtractor : ITextExtractor
{
    public async Task<string?> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        var result = new StringBuilder();
        using var document = PdfDocument.Open(filePath);
        foreach (var page in document.GetPages())
        {
            result.AppendLine(ContentOrderTextExtractor.GetText(page));
        }

        return result.ToString();
    }
}
