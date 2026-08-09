using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Lore.Core.TextExtractors;

public class DocxExtractor : ITextExtractor
{
    public Task<string?> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        using var doc = WordprocessingDocument.Open(filePath, false);
        var body = doc.MainDocumentPart?.Document?.Body;
        if (body == null)
        {
            return Task.FromResult<string?>(string.Empty);
        }

        var sb = new StringBuilder();
        foreach (var element in body.ChildElements)
        {
            if (element is Paragraph paragraph)
            {
                AppendParagraphText(sb, paragraph);
            }
            else if (element is Table table)
            {
                AppendTableText(sb, table);
            }
            else if (element is SdtBlock sdtBlock)
            {
                foreach (var innerPara in sdtBlock.Descendants<Paragraph>())
                {
                    AppendParagraphText(sb, innerPara);
                }
            }
        }

        return Task.FromResult<string?>(sb.ToString());
    }

    private static void AppendParagraphText(StringBuilder sb, Paragraph paragraph)
    {
        var text = paragraph.InnerText?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            sb.AppendLine(text);
        }
    }

    private static void AppendTableText(StringBuilder sb, Table table)
    {
        foreach (var row in table.Elements<TableRow>())
        {
            var rowCells = row.Elements<TableCell>()
                .Select(cell => cell.InnerText.Trim())
                .Where(t => !string.IsNullOrEmpty(t));

            var rowText = string.Join(" | ", rowCells);
            if (!string.IsNullOrWhiteSpace(rowText))
            {
                sb.AppendLine(rowText);
            }
        }
        sb.AppendLine();
    }
}
