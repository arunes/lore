using System.ClientModel;
using Lore.Core.Services;
using Lore.Common.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace Lore.Core.LLM;

public interface IChatClientFactory
{
    Task<IChatClient> CreateClientAsync(CancellationToken cancellationToken = default);
}

public class ChatClientFactory(IUserSettingsService userSettings) : IChatClientFactory
{
    public async Task<IChatClient> CreateClientAsync(
        CancellationToken cancellationToken = default
    )
    {
        var aiEndpoint = userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIUrl);
        var aiAuthKey = userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIKey);
        var aiModel = userSettings.GetSetting<string>(UserSettingsType.AIBackendAPIModel);

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri(aiEndpoint)
        };

        return new ChatClient(aiModel, new ApiKeyCredential(aiAuthKey), clientOptions).AsIChatClient();
    }
}
