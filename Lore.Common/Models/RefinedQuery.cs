using System.ComponentModel;

namespace Lore.Common.Models;

public record RefinedQuery(
    [property: Description(
        "Concise, highly relevant keywords/phrases, exact identifiers, or terms (no natural language noise)."
    )]
        string FTSKeywords,
    [property: Description(
        "A query targeting high-level document context, category names, summaries, or broad document themes."
    )]
        string MetadataQuery,
    [property: Description(
        "A dense semantic query targeting specific paragraphs, code blocks, or explanations inside document body text."
    )]
        string PassageQuery
);
