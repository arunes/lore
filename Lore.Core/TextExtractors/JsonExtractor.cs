using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Lore.Core.Logging;

namespace Lore.Core.TextExtractors;

[SupportedExtensions(".json", ".jsonl", ".ndjson", ".gdoc", ".gsheet")]
public class JsonExtractor : ITextExtractor
{
    private readonly ILogger<JsonExtractor> _logger;

    public JsonExtractor(ILogger<JsonExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<string?> ExtractTextAsync(
        string filePath,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            await using var stream = File.OpenRead(filePath);
            using var doc = await JsonDocument.ParseAsync(
                stream,
                cancellationToken: cancellationToken
            );

            var sb = new StringBuilder();
            FlattenElement(doc.RootElement, string.Empty, sb);

            return sb.ToString();
        }
        catch (JsonException ex)
        {
            _logger.ExtractionWarning(filePath, "json", ex);
            return null;
        }
    }

    private static void FlattenElement(JsonElement element, string currentPath, StringBuilder sb)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    var path = string.IsNullOrEmpty(currentPath)
                        ? property.Name
                        : $"{currentPath}.{property.Name}";

                    FlattenElement(property.Value, path, sb);
                }
                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FlattenElement(item, $"{currentPath}[{index++}]", sb);
                }

                break;

            case JsonValueKind.String:
            case JsonValueKind.Number:
            case JsonValueKind.True:
            case JsonValueKind.False:
                var value = element.ToString()?.Trim();
                if (!string.IsNullOrEmpty(value))
                {
                    sb.AppendLine($"{currentPath}: {value}");
                }
                break;
        }
    }
}