using System.Text.Json;
using Lore.Common;
using Lore.Common.Models;
using Microsoft.Extensions.Logging;

namespace Lore.Core.Settings;

public interface ISettingsPresetService
{
    Task<IReadOnlyList<SettingsPreset>> GetPresetsAsync(CancellationToken cancellationToken = default);
}

public sealed record SettingsPreset(
    string Name,
    IReadOnlyDictionary<string, string> Values);

public sealed class SettingsPresetService(
    ILogger<SettingsPresetService> logger)
    : ISettingsPresetService
{
    public async Task<IReadOnlyList<SettingsPreset>> GetPresetsAsync(
        CancellationToken cancellationToken = default)
    {
        string presetsDirectory = LorePaths.PresetsDir;
        if (!Directory.Exists(presetsDirectory))
        {
            return [];
        }

        Dictionary<UserSettingsType, SettingDefinition> definitions = SettingsCatalog.All
            .ToDictionary(definition => definition.Key);
        List<SettingsPreset> presets = [];

        foreach (string filePath in Directory.EnumerateFiles(presetsDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await using FileStream stream = File.OpenRead(filePath);
                using JsonDocument document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    logger.LogWarning("Ignoring settings preset '{PresetPath}' because it is not a JSON object", filePath);
                    continue;
                }

                Dictionary<string, string> values = [];
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (!Enum.TryParse(property.Name, ignoreCase: false, out UserSettingsType key)
                        || !definitions.TryGetValue(key, out SettingDefinition? definition)
                        || definition.IsSecret
                        || property.Value.ValueKind is JsonValueKind.Null
                    )
                    {
                        continue;
                    }

                    values[key.ToString()] = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()!
                        : property.Value.ToString();
                }

                presets.Add(new SettingsPreset(
                    Path.GetFileNameWithoutExtension(filePath),
                    values));
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Ignoring invalid settings preset '{PresetPath}'", filePath);
            }
        }

        return presets.OrderBy(preset => preset.Name).ToList();
    }
}
