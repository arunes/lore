using System.Text.Json;

namespace Lore.Common.Extensions;

public static class JsonExtensions
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static T? DeserializeJson<T>(this string json, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Deserialize<T>(json, options ?? Options);
    }
}