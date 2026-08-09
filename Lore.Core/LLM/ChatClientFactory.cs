using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace Lore.Core.LLM;

public interface IChatClientFactory
{
    Task<IChatClient> CreateClientAsync(CancellationToken cancellationToken = default);
}

public class ChatClientFactory : IChatClientFactory
{
    public async Task<IChatClient> CreateClientAsync(
        CancellationToken cancellationToken = default
    )
    {
        // TODO: Fetch current settings saved by the user from DB

        var clientOptions = new OpenAIClientOptions
        {
            Endpoint = new Uri("http://127.0.0.1:1234/v1"),
        };

        // classification: qwen/qwen3-4b-2507
        // query refinment & search: qwen/qwen3-14b

        // qwen2.5-3b-instruct: 43 seconds
        // llama-3.2-3b-instruct: 44 seconds
        // granite-4.1-3b: 39 seconds

        return new ChatClient(
            "openai/gpt-oss-20b",
            new ApiKeyCredential("lm-studio"),
            clientOptions
        ).AsIChatClient();
    }
}
