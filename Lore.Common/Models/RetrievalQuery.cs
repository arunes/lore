using System.ComponentModel;

namespace Lore.Common.Models;

public class RetrievalQuery
{
    [Description(
        "True only when answering the latest message requires information from the indexed documents that is not already available in the conversation.")]
    public bool NeedsRetrieval { get; set; }

    [Description(
        "A short, standalone semantic search query describing the information to find in the documents. Null when retrieval is not needed.")]
    public string? SearchQuery { get; set; }

    [Description(
        "2-8 specific keywords or phrases likely to appear in relevant documents. Empty when retrieval is not needed.")]
    public List<string> FTSTerms { get; set; } = [];
}