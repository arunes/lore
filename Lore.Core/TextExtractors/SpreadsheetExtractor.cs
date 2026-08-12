using System.Text;
using ExcelDataReader;
using Lore.Core.TextExtractors;

[SupportedExtensions(".xlsx", ".xls", ".ods")]
public class SpreadsheetExtractor : ITextExtractor
{
    public Task<string?> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        using var stream = File.OpenRead(filePath);
        using var reader = ExcelReaderFactory.CreateReader(stream);

        var sb = new StringBuilder();
        do
        {
            sb.AppendLine($"--- Sheet: {reader.Name} ---");
            while (reader.Read())
            {
                var row = Enumerable
                    .Range(0, reader.FieldCount)
                    .Select(i => reader.GetValue(i)?.ToString()?.Trim())
                    .Where(val => !string.IsNullOrEmpty(val));

                var rowText = string.Join(" | ", row);
                if (!string.IsNullOrWhiteSpace(rowText))
                    sb.AppendLine(rowText);
            }
        } while (reader.NextResult());

        return Task.FromResult<string?>(sb.ToString());
    }
}
