# AGENTS.md — Lore repository guidance

## Repository Overview

Lore is a local object-retrieval and retrieval-augmented-generation application. The backend is an ASP.NET Core / .NET 10 application and the frontend is a React, TypeScript, and Vite application served as static files by the backend in production.

The main backend projects are layered as follows:

- `Lore.Common` — shared request/response records, settings metadata, enums, paths, JSON/text helpers, and other dependency-light contracts.
- `Lore.Data` — EF Core/SQLite persistence, migrations, entity models, SQLite FTS5 setup, and the `sqlite-vec` vector table setup.
- `Lore.Core` — ingestion pipeline, extractors, local embedding, classification, retrieval, RAG services, settings, logging, and telemetry.
- `Lore.App` — application composition root, HTTP routes, exception handling, OpenTelemetry registration, static-file hosting, and startup.
- `Lore.UI` — React/TypeScript/Vite client. Its production build is copied into `Lore.App/wwwroot` by `Dockerfile`.
- `smartcomponents` — local source projects used by Lore for ONNX embeddings and inference support. Treat these as repository code, not as an external black box.

### End-to-end data flow

1. `StartupService` initializes settings and the embedding classification cache, resumes incomplete files, scans enabled `FileSource` directories, and starts `FileSystemWatcher` instances through `DirectoryWatcher`.
2. File events enter an unbounded `Channel<FileArrivalRequest>`. `ChannelProcessor<TRequest>` drains each channel in batches and invokes the matching `IChannelService<TRequest>`.
3. `FileArrivalService` checks file existence and modification time, computes a SHA-256 hash, upserts the `FileEntry`, and sends new/changed files to `TextExtractService`.
4. `TextExtractService` resolves an extractor by normalized extension, extracts text, normalizes it with `CleanTextForRAG`, updates `FileEntry.Content` and `FileProcessStatus`, and hands successful files to `FileClassifyService`.
5. `FileClassifyService` embeds the file name/path/extension/content snippet and selects the closest seeded `PrimaryCategory` and `DocumentType` from `EmbeddingCache`.
6. `ChunkingService` splits cleaned content using Semantic Kernel `TextChunker`: 100-line segments, then 300-token-ish paragraph chunks with 30 overlap. It persists `FileEntryChunk` rows and hands files to `VectorizeService`.
7. `VectorizeService` embeds chunk text plus file metadata using `SmartComponents.LocalEmbeddings.LocalEmbedder`, then writes 384-dimensional vectors to the SQLite `vec_file_chunks` virtual table in a transaction. A successful file becomes `Done`.
8. `RetrievalService` runs FTS5 and sqlite-vec searches in parallel, fuses result ranks with weighted Reciprocal Rank Fusion (`k = 60`), and loads chunk text grouped by file.
9. `TraditionalRAGService` optionally asks an OpenAI-compatible `IChatClient` to create a structured `RetrievalQuery`, retrieves context, and streams the final answer through `IAsyncEnumerable<string>`. `AgenticRAGService` uses Semantic Kernel function calling and `KernelRetrievalTools` for search, metadata, directory, and file-content tools.
10. `Lore.App/Routes.cs` selects the configured keyed `IRAGService`, streams response text over `POST /api/chat`, and exposes settings APIs.

## Architectural Constraints

These are hard rules for AI-generated changes.

### Boundaries and dependency direction

- Keep dependency direction intact: `Lore.App` may depend on `Lore.Core`, `Lore.Data`, and `Lore.Common`; `Lore.Core` may depend on `Lore.Data`, `Lore.Common`, and the local SmartComponents projects; `Lore.Data` may depend on `Lore.Common`; `Lore.Common` must remain infrastructure-independent.
- Do not make HTTP routes, RAG services, extractors, or UI code issue ad hoc persistence queries when an existing service or interface owns that responsibility.
- Do not bypass `IRetrievalService` to query `file_chunks_fts`, `vec_file_chunks`, or chunk metadata from RAG orchestration. Add a retrieval abstraction when a new retrieval capability is needed.
- Do not put provider-specific database or LLM code into shared contracts. Keep SQLite/sqlite-vec code in `Lore.Data`/`Lore.Core.Retrieval` and OpenAI/Semantic Kernel code in `Lore.Core.RAG`.
- Register new services in the appropriate `*Registration` extension rather than adding scattered registrations in `Program.cs`.
- Preserve the keyed `IRAGService` design (`Traditional` and `Agentic`) and resolve the backend through `IRAGFactory`.

### Ingestion and pipeline rules

- Treat the five pipeline stages and their channels as a state machine: arrival → extraction → classification → chunking → vectorization. A stage must only emit the next request after its durable database update succeeds.
- Use the existing `IChannelService<TRequest>` and `ChannelProcessor<TRequest>` pattern for new stages. Respect cancellation, batching, and the configured `GetBatchSize()`; do not create unmanaged per-file background tasks.
- Every request handed between stages must preserve its `TraceParent` when possible. New stages should create an internal span with `TracingHelper.StartStageSpan` and record stage/file tags and metrics.
- A file is searchable only after both `FileEntryChunk` rows and corresponding `vec_file_chunks` rows are present. Do not mark a file `Done` early.
- Reprocessing must be idempotent. Before inserting replacement chunks, remove or replace the previous chunks for that file; before writing vectors, remove the old vector rows for those chunk IDs. Also clean vector rows when files/chunks are deleted, because the sqlite-vec virtual table is not protected by the relational cascade.
- Failed, unsupported, and empty files must remain observable through their `FileProcessStatus` and logs. Do not convert failures into `Done`, silently drop them, or enqueue failed records into a later stage.
- File modification time is currently used as an arrival fast path and the hash is stored. Do not assume that either check alone proves content identity; if deduplication behavior changes, define and test the semantics explicitly.
- Normalize extensions and paths consistently. Extension matching should be case-insensitive, and directory authorization must use normalized full paths with a path-boundary check, not a naive string prefix.

### Persistence and vector-store rules

- Use `IDbContextFactory<LoreDbContext>` for long-lived/background and concurrent work. Do not share one `DbContext` across parallel tasks.
- Be deliberate about service lifetimes. A singleton must not capture a scoped `LoreDbContext`; use a context factory or change the lifetime. This applies especially when modifying `UserSettingsService`, `EmbeddingCache`, or other singleton registrations.
- Use EF Core LINQ for ordinary relational queries and parameterized SQL for FTS5/sqlite-vec operations. Never interpolate untrusted search terms, paths, table names, or limits into raw SQL.
- Keep the vector dimension synchronized across the embedding model, `EnsureVectorTablesCreatedAsync`, vector writes, and vector queries. The current sqlite-vec schema is `float[384]`; changing the model requires a deliberate rebuild/migration plan.
- Initialize relational migrations, sqlite-vec, and FTS5 triggers through `DbInitializerHostedService`/`DbContextExtensions`. Do not require a developer to manually create virtual tables for normal startup.
- If the EF model changes, add a migration under `Lore.Data/Migrations` and verify both a fresh database and an existing database. Do not edit an old applied migration to repair a schema.
- Treat an empty query and an empty result as valid states. Skip or safely handle vector embedding for an empty semantic query, allow FTS-only/vector-only operation when one side is empty, and return an empty list rather than throwing.

### LLM, embedding, and RAG rules

- The configured backend is OpenAI-compatible (`Microsoft.Extensions.AI.OpenAI`/`OpenAI` client) and is also used by Semantic Kernel. Do not assume the endpoint is OpenAI-hosted; preserve configurable URL, API key, and model support for LM Studio and similar local servers.
- Use `Microsoft.Extensions.AI` for the traditional chat path and Semantic Kernel only for the agentic path. Do not mix chat-history types or provider abstractions without a clear adapter.
- Preserve streaming APIs: RAG services return `LoreChatResponse` with `IAsyncEnumerable<string>`, and async streams must honor `CancellationToken` with `WithCancellation`/`EnumeratorCancellation`.
- Structured retrieval-query generation must retain a deterministic fallback to a raw prompt query when the LLM returns invalid JSON, an unsupported response, or an error. Do not make retrieval unavailable merely because query planning failed.
- Keep retrieved context clearly delimited from the user question. Preserve file path, category, document type, and chunk ordering when constructing context.
- New Semantic Kernel tools must have precise descriptions, cancellation support, setting-based enablement, bounded results, and authorization checks. Tool failures should be safe and understandable to the model; never expose arbitrary filesystem access.
- Do not log API keys, full prompts, full document contents, or sensitive OCR output. Log identifiers, counts, durations, and safe paths only as appropriate.

## C# Style and Formatting

- Target `net10.0`, with nullable reference types and implicit usings enabled. Use modern C# supported by the project.
- Prefer primary constructors for dependency injection, as used throughout `Lore.Core`, `Lore.Data`, and `Lore.App`. Keep constructor-injected dependencies explicit and small.
- Use records for immutable pipeline messages and DTO-like contracts. Use required properties and nullable annotations to express entity invariants accurately.
- Use `async`/`await` for I/O. Suffix asynchronous methods with `Async`. Prefer `IAsyncEnumerable<T>` for streaming or large sequential database results where the existing API uses it.
- Pass a `CancellationToken` through every database, file, channel, embedding, LLM, and stream operation. Do not use `.Result`, `.Wait()`, `async void`, or fire-and-forget work without a lifecycle owner.
- Follow `.editorconfig`: four spaces, LF endings, braces for control flow, `System` usings first and outside namespaces, PascalCase for public types/methods/properties, `IPascalCase` interfaces, `_camelCase` private fields, and camelCase locals/parameters.
- Prefer collection expressions (`[]`), pattern matching, `ArgumentNullException.ThrowIfNull`, generated regexes, and `var` only where the type is apparent. Do not add broad suppressions to bypass analyzers; document narrowly scoped Semantic Kernel experimental warnings as the existing chunker does.
- Use source-generated `[LoggerMessage]` partial logging methods for recurring operational events. Include stable event IDs and structured properties. Add telemetry for new expensive pipeline, retrieval, and LLM operations using the existing `LoreActivitySource`/`LoreMetrics` conventions.
- Comments and XML documentation should explain non-obvious architectural decisions, provider quirks, security boundaries, retry/idempotency behavior, and vector dimension assumptions. Do not add comments that merely restate code.

## Testing and Verification Rules

There is currently no committed solution file or test project in the repository. Treat every change as requiring build, formatting, analyzer, and focused runtime verification until automated tests are added.

### Required backend checks

```bash
dotnet restore Lore.App/Lore.App.csproj
dotnet build Lore.App/Lore.App.csproj --no-restore
dotnet format style Lore.App/Lore.App.csproj --verify-no-changes --no-restore
dotnet format analyzers Lore.App/Lore.App.csproj --verify-no-changes --no-restore
```

These are the same checks used by `.github/workflows/build.yml`. If a change touches migrations, also run the relevant `dotnet ef` command from the `Lore.Data` project and inspect the generated migration; do not rely on compilation alone.

### UI and container checks

```bash
cd Lore.UI
pnpm install --frozen-lockfile
pnpm build
pnpm lint
```

For production integration, build the repository `Dockerfile` and verify that the UI is copied into `Lore.App/wwwroot`, the application listens on port 8080, and `LORE_DATA_ROOT` is writable.

### RAG and database verification

- Run with a temporary `LORE_DATA_ROOT` so tests do not mutate a developer's real `lore.db`:
  `LORE_DATA_ROOT=/tmp/lore-test dotnet run --project Lore.App/Lore.App.csproj`.
- Configure an OpenAI-compatible local backend (for example, LM Studio) with the settings API or the seeded settings defaults. Verify both traditional and agentic backend selection separately.
- Add a small text fixture under a temporary watched source and verify status transitions: `Pending` → `TextExtracted` → `Classified` → `ChunksCreated` → `Done`.
- Check relational state (`files`, `file_chunks`) and virtual-table state (`vec_file_chunks`, `file_chunks_fts`) after ingestion. The vector row count should match the chunk rows for successfully indexed files; FTS rows/triggers should reflect chunk inserts, updates, and deletes.
- Exercise an exact keyword query, a semantic query, an empty query, a query with no matches, and a query when only FTS or only vector results exist. Confirm hybrid fusion does not throw and preserves the configured result limit.
- Verify changed and deleted files do not leave stale chunks or vectors and that restarting the application resumes incomplete statuses rather than duplicating work.
- For LLM tests, use a local/mock OpenAI-compatible provider where possible. Verify invalid retrieval-query JSON falls back to the raw query, client cancellation stops the stream, no-results context is explicit, and disabled agentic tools cannot access data.
- When observability is configured, check spans under `pipeline.*`, `retrieval/hybrid_search`, and `chat/*`, plus metrics under `pipeline.*` and `rag.*`. Never use production secrets or sensitive documents in diagnostics.

## Common Anti-Patterns to Avoid

- Directly querying SQLite virtual tables from a route, UI-facing handler, or RAG service instead of going through `IRetrievalService`.
- Registering a class with a scoped `LoreDbContext` dependency as a singleton, or sharing a context between concurrent batch tasks.
- Inserting new chunks on reprocessing without removing the old chunks, which creates duplicate searchable content and stale vector rows.
- Catching vector-write exceptions, rolling back, and then reporting normal completion without setting `VectorizationFailed`, preserving the failure for resume/diagnostics, or rethrowing when the stage contract requires it.
- Enqueuing classification failures or empty/unsupported extraction results into chunking/vectorization.
- Calling `Embed` for a null/blank search query or assuming an embedding exists when the local model is unavailable; handle no-query and no-index cases explicitly.
- Hardcoding a different vector dimension, embedding model, chunk size, RRF limit, or search weight in a new component instead of using the current contract/settings and documenting intentional changes.
- Building raw FTS or vector SQL with string concatenation. Use EF parameterization and validate/bound numeric limits.
- Authorizing a file with `path.StartsWith(sourcePath)` alone. Normalize paths and enforce a directory boundary to prevent sibling-prefix escapes.
- Adding a new extractor without `ITextExtractor`, `SupportedExtensionsAttribute`, cancellation-aware I/O, and registration verification.
- Blocking async code, using fire-and-forget tasks in hosted services, swallowing `OperationCanceledException` incorrectly, or ignoring the request cancellation token.
- Logging full prompts, document text, API keys, or OCR results; use structured, privacy-conscious summaries instead.
- Replacing the existing primary-constructor/DI style with service location, static mutable state, or a second provider abstraction without an explicit architectural reason.
- Editing generated `bin`/`obj` files, the SQLite database, UI `node_modules`, or an already-applied migration as source changes.
