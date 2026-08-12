using System.ComponentModel;
using Lore.Common.Extensions;
using Lore.Common.Models;
using Lore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Lore.Core.Settings;

public class UserSettingsService(ILogger<UserSettingsService> logger, LoreDbContext dbContext) : IUserSettingsService
{
    private readonly Dictionary<UserSettingsType, string?> _settings = [];

    public T GetSetting<T>(UserSettingsType settingsType)
    {
        if (!_settings.TryGetValue(settingsType, out string? value))
        {
            value = settingsType.GetAttribute<DefaultValueAttribute>()!.Value!.ToString();
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new KeyNotFoundException($"The value of {settingsType} setting cannot be empty!");
        }

        return ConvertValue<T>(value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        var allSettings = await dbContext.Settings.ToListAsync(cancellationToken);

        foreach (var setting in allSettings)
        {
            if (!Enum.TryParse(setting.Key, out UserSettingsType settingKey))
            {
                continue;
            }

            _settings[settingKey] = setting.Value;
        }
    }

    public static T ConvertValue<T>(object value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (targetType.IsEnum)
        {
            if (value is string stringValue)
            {
                return (T)Enum.Parse(targetType, stringValue, ignoreCase: true);
            }

            // Handles converting numeric types (e.g., int, byte) to the Enum type
            return (T)Enum.ToObject(targetType, value);
        }

        return (T)Convert.ChangeType(value, targetType);
    }
}