using Microsoft.Extensions.AI;

namespace Lore.Common.Models;

public record LoreChatRequest(Guid? ChatId, string Prompt, bool RefinePrompt = false);