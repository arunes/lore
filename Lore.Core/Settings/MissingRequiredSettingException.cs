using Lore.Common.Models;

namespace Lore.Core.Settings;

public sealed class MissingRequiredSettingException : Exception
{
    public UserSettingsType Setting { get; }

    public MissingRequiredSettingException(UserSettingsType setting, string displayName)
        : base($"Setting '{displayName}' ({setting}) is required but is not configured. Open Settings and set it to continue.")
    {
        Setting = setting;
    }
}
