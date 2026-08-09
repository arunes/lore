using System.Text.Json;

namespace Lore.Common.Helpers;

public static class JsonHelpers
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };
}
