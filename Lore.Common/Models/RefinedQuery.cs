using System.ComponentModel;

namespace Lore.Common.Models;

public record RefinedQuery(
    [property: Description(
        "Concise, highly relevant keywords/phrases (no natural language noise). Set to 'NO_SEARCH' if no search is required."
    )]
    string FTSKeywords,

    [property: Description(
        "A query targeting high-level document context, category names, summaries, or broad document themes. Set to 'NO_SEARCH' if no search is required."
    )]
    string MetadataQuery,

    [property: Description(
        "A dense semantic query targeting specific paragraphs or explanations. Set to 'NO_SEARCH' if no search is required."
    )]
    string PassageQuery
);