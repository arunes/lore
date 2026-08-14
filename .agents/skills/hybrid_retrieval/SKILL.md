# Skill: Hybrid Retrieval and Vector Store

## Skill Name & Purpose

**Hybrid Retrieval and Vector Store** owns all document search against SQLite FTS5 and sqlite-vec, including query normalization, safe SQL parameterization, parallel lexical/semantic searches, weighted Reciprocal Rank Fusion, and chunk hydration. It is the only domain that should know how the two virtual tables are queried.

## Key Classes & Interfaces

- `Lore.Core/Retrieval/IRetrievalService.cs` — retrieval boundary: `RetrieveDocumentChunksAsync` and `GetChunkContentsAsync`.
- `Lore.Core/Retrieval/RetrievalService.cs` — FTS5 search, vector search, RRF fusion, and chunk grouping.
- `Lore.Core/Retrieval/RetrievalTextExtensions.cs` — text cleanup via `CleanTextForRAG`.
- `Lore.Common/Models/RetrievalQuery.cs` — `NeedsRetrieval`, semantic `SearchQuery`, and `FTSTerms`.
- `Lore.Data/DbContextExtensions.cs` — loads sqlite-vec and creates `vec_file_chunks`, FTS5 tables, and triggers.
- `Lore.Data/Models/FileEntryChunk.cs` and `FileEntry.cs` — relational chunk and metadata source.
- `Lore.Core/Settings/UserSettingsService.cs` / `UserSettingsType` — result limit and FTS/vector weights.

The current algorithm obtains up to `MaxNumberSearchResults` from each stream, then calculates `weight * 1 / (60 + rank + 1)` and takes the configured final limit.

## Inputs & Outputs

**Inputs**

- A `RetrievalQuery` with a nullable/blank semantic query and zero or more FTS terms.
- Configured `MaxNumberSearchResults`, `SearchFTSWeight`, and `SearchVectorWeight`.
- A list of chunk IDs returned by the fused search for hydration.

**Outputs**

- `List<int>` of unique chunk IDs ordered by fused relevance.
- `List<DocumentChunkFile>` grouped by file with path, category, document type, chunk text, and chunk index.
- Empty lists for empty queries, no matches, absent lexical results, absent vector results, or no requested IDs.

## Step-by-Step Execution Rules

1. Keep `IRetrievalService` as the boundary. RAG orchestration and Semantic Kernel tools request retrieval through it; they must not know virtual-table SQL.
2. Normalize a semantic query before embedding. Blank/whitespace input must not be sent to `LocalEmbedder`; treat it as no vector stream.
3. Normalize FTS terms by trimming, removing blanks, de-duplicating case-insensitively, and escaping FTS5 phrase quotes. Preserve the current OR phrase behavior unless a search feature explicitly changes it.
4. Use a vector-aware context from `CreateVectorDbContextAsync` so sqlite-vec is loaded on the connection. Dispose it asynchronously.
5. Keep FTS and vector searches concurrent where the provider permits it, but do not share a context across operations if the selected provider/context configuration cannot safely support concurrent commands; use separate factory-created contexts in that case.
6. Parameterize all user-derived FTS expressions, vector JSON, and numeric limits. Validate that limits are positive and bounded before they reach SQL. Never concatenate raw query text into SQL.
7. Handle each stream independently. If FTS terms are empty, return no FTS stream; if semantic query is empty, return no vector stream. Fusion must work with either stream alone.
8. Deduplicate chunk IDs during RRF by accumulating scores. Preserve deterministic descending score ordering and apply the final configured limit once.
9. Hydrate only IDs returned by retrieval, use `AsNoTracking`, and group results by file. Return chunks ordered by `ChunkIndex` when building answer context.
10. If virtual tables are missing, embeddings have a mismatched dimension, or SQL fails, surface a useful operational failure and log counts/duration; do not silently return plausible but incomplete results.
11. Record retrieval spans and metrics (`fts_count`, `vector_count`, `fused_count`, duration). Do not log search contents or returned sensitive text.
12. Add tests for blank input, FTS-only, vector-only, duplicate IDs, no matches, result limits, escaping quotes, and a database with zero indexed chunks.

## Example Usage / Pattern

```csharp
public async Task<List<int>> RetrieveDocumentChunksAsync(
    RetrievalQuery query,
    CancellationToken cancellationToken)
{
    string semanticText = CleanSearchQuery(query.SearchQuery);
    string fts = FormatFtsQuery(query.FTSTerms);
    int limit = Math.Clamp(
        userSettings.GetSetting<int>(UserSettingsType.MaxNumberSearchResults),
        1,
        1000);

    List<int> lexical = string.IsNullOrEmpty(fts)
        ? []
        : await QueryFtsAsync(fts, limit, cancellationToken);

    List<int> semantic = string.IsNullOrEmpty(semanticText)
        ? []
        : await QueryVectorAsync(
            JsonSerializer.Serialize(embedder.Embed(semanticText).Values.ToArray()),
            limit,
            cancellationToken);

    return FuseWithRrf(lexical, semantic, limit);
}
```

The helper methods in a real implementation must use provider parameters and the existing `CreateVectorDbContextAsync` abstraction rather than interpolating the example values into SQL.
