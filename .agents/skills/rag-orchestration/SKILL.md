---
name: rag-orchestration
description: Guides Lore chat backend selection, retrieval-augmented prompt assembly, Semantic Kernel tools, OpenAI-compatible clients, streaming, fallbacks, and chat caching.
---

# Skill: RAG Orchestration and LLM Streaming

## Skill Name & Purpose

**RAG Orchestration and LLM Streaming** owns chat request handling after the HTTP boundary: backend selection, conversation history, retrieval-query planning, context assembly, Semantic Kernel tool calling, OpenAI-compatible client creation, streaming, fallback behavior, and short-lived chat caching.

## Key Classes & Interfaces

- `Lore.Core/RAG/IRAGService.cs` — common `ChatAsync` contract.
- `Lore.Core/RAG/TraditionalRAGService.cs` — structured retrieval planning, hybrid retrieval, context prompt assembly, and `Microsoft.Extensions.AI` streaming.
- `Lore.Core/RAG/AgenticRAGService.cs` — Semantic Kernel streaming with automatic function choice.
- `Lore.Core/RAG/IRAGFactory.cs` and `RAGFactory.cs` — keyed backend selection from `AIBackendRAGServiceType`.
- `Lore.Core/RAG/IKernelFactory.cs` and `KernelFactory.cs` — OpenAI-compatible Semantic Kernel construction.
- `Lore.Core/RAG/KernelRetrievalTools.cs` — guarded retrieval, metadata, directory, and full-content tools.
- `Lore.Common/Models/LoreChatRequest.cs`, `LoreChatResponse.cs`, and `RetrievalQuery.cs` — API and planner contracts.
- `Lore.App/Routes.cs` — HTTP streaming endpoint and cancellation propagation.
- `Lore.Core/Settings/UserSettingsService.cs` — prompts, endpoint, credentials, model, temperatures, and tool flags.

## Inputs & Outputs

**Inputs**

- `LoreChatRequest(Guid? ChatId, string Prompt)`.
- A configured OpenAI-compatible URL, model, API key, prompts, temperatures, and selected RAG backend.
- Conversation history held in `IMemoryCache` for 15 minutes.

**Outputs**

- `LoreChatResponse(Guid ChatId, IAsyncEnumerable<string> LLMResponseStream)`.
- Traditional mode: a final streamed answer using retrieved, grouped chunk context when retrieval is needed.
- Agentic mode: a streamed Semantic Kernel response that may invoke enabled retrieval tools.
- A safe fallback retrieval query when structured LLM planning fails.

## Step-by-Step Execution Rules

1. Keep `IRAGService` provider-agnostic and return the existing `LoreChatResponse` shape. Do not make routes know whether the backend is traditional or agentic.
2. Resolve the backend through `IRAGFactory` and keyed registrations. Add a backend only by extending the enum/settings contract, registration, and factory behavior together.
3. Construct OpenAI-compatible clients from `IUserSettingsService`; preserve custom endpoints for local servers. Never hardcode credentials, endpoints, models, or prompts.
4. In traditional mode, use a structured `RetrievalQuery` response schema. A planner failure, invalid JSON, blank fields, or provider incompatibility must fall back to `NeedsRetrieval = true`, the raw prompt as `SearchQuery`, and prompt terms as `FTSTerms`.
5. Call `IRetrievalService` only when the query requires retrieval. A retrieval-required query with zero results must produce explicit no-context markup, not an empty ambiguous prompt.
6. Assemble context with clear `<retrieved_context>`, `<file>`, and `<chunk>` delimiters. Preserve source path/category/document type and sort chunks by `ChunkIndex`.
7. Use `IAsyncEnumerable<string>` and `[EnumeratorCancellation]`. Pass cancellation into provider calls and use `WithCancellation` while consuming updates. Do not buffer the response before yielding tokens.
8. Record the assistant history only after the stream completes successfully. Keep the existing 15-minute cache boundary and use distinct cache keys for traditional and agentic backends.
9. In agentic mode, create a fresh kernel through `IKernelFactory`, use `FunctionChoiceBehavior.Auto`, and keep retrieval functions in `KernelRetrievalTools`. Do not inject arbitrary services as tools.
10. Every tool must check its `UserSettingsType` enablement flag, bound result sizes, accept cancellation, and enforce source-path authorization before reading a file. Avoid naive prefix authorization and avoid returning unrestricted filesystem results.
11. Catch initialization/provider failures at the stream boundary and log a safe structured event. Do not expose API keys, complete prompts, document text, or raw provider errors in HTTP output.
12. Test traditional no-retrieval, retrieval success, retrieval empty, invalid planner JSON, provider failure, client cancellation, and multi-turn cache behavior. Test each agentic tool both enabled and disabled.

## Example Usage / Pattern

```csharp
private async IAsyncEnumerable<string> StreamFromLlmAsync(
    IChatClient chatClient,
    List<ChatMessage> history,
    string userMessage,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    List<ChatMessage> messages = [
        .. history,
        new(ChatRole.User, userMessage)
    ];

    IAsyncEnumerable<ChatResponseUpdate> updates =
        chatClient.GetStreamingResponseAsync(
            messages,
            new ChatOptions { Temperature = temperature },
            cancellationToken: cancellationToken);

    await foreach (ChatResponseUpdate update in updates.WithCancellation(cancellationToken))
    {
        if (!string.IsNullOrEmpty(update.Text))
        {
            yield return update.Text;
        }
    }
}
```

Use `TraditionalRAGService` and `AgenticRAGService` as the canonical patterns for activity spans, structured logs, metrics, cache updates, and provider-specific response types.
