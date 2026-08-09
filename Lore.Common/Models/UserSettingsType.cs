using System.ComponentModel;

namespace Lore.Common.Models;

public enum UserSettingsType
{
    [DefaultValue("http://127.0.0.1:1234/v1")]
    AIBackendAPIUrl,

    [DefaultValue("lm-studio")]
    AIBackendAPIKey,

    [DefaultValue("")]
    AIBackendAPIModel
}