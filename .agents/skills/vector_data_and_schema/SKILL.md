# Skill: Vector Data, SQLite Schema, and Persistence

## Skill Name & Purpose

**Vector Data, SQLite Schema, and Persistence** owns the durable relational model and the SQLite-specific virtual-table lifecycle that makes Lore searchable. It covers EF Core entities and migrations, FTS5 triggers, sqlite-vec loading, vector table dimension, transaction behavior, and safe database initialization.

## Key Classes & Interfaces

- `Lore.Data/LoreDbContext.cs` — EF Core sets, conventions, indexes, relationships, and seeded categories/types.
- `Lore.Data/Models/FileEntry.cs` — indexed file and `FileProcessStatus` state.
- `Lore.Data/Models/FileEntryChunk.cs` — chunk rows with cascade relationship to files.
- `Lore.Data/Models/FileSource.cs` and `Setting.cs` — source and application configuration persistence.
- `Lore.Data/DbContextExtensions.cs` — `CreateVectorDbContextAsync`, sqlite-vec loading, vector table creation, FTS5 table and trigger creation.
- `Lore.Data/DbInitializerHostedService.cs` — migrations and virtual-table initialization at startup.
- `Lore.Data/ServiceHelpers.cs` — SQLite connection and DI registration.
- `Lore.Data/Migrations/*` — versioned relational schema changes.
- `Lore.Core/Pipeline/VectorizeService.cs` — vector-row writes and file completion transaction.
- `Lore.Core/Retrieval/RetrievalService.cs` — read-side FTS5/sqlite-vec usage.

## Inputs & Outputs

**Inputs**

- EF Core entity/model changes and migration requests.
- SQLite database at `LorePaths.DatabasePath`, normally under `LORE_DATA_ROOT`.
- Chunks and `ReadOnlyMemory<float>` embeddings from the ingestion pipeline.
- FTS and vector query parameters from retrieval.

**Outputs**

- A migrated relational database with snake_case tables/columns.
- `vec_file_chunks(chunk_id INTEGER PRIMARY KEY, embedding float[384])`.
- `file_chunks_fts` contentless/external-content FTS5 index and insert/update/delete triggers.
- Durable relational and virtual-table state that can be resumed and queried after restart.

## Step-by-Step Execution Rules

1. Decide whether a change is relational, virtual-table, initialization, or application-level. Keep schema lifecycle code in `Lore.Data`; keep retrieval algorithm code in `Lore.Core.Retrieval`.
2. For relational entity changes, update the model and create a new EF migration. Inspect `Up` and `Down`, verify foreign keys/indexes, and test against both a fresh and an existing database.
3. Never edit an already-applied migration to repair production/local state. Never commit local SQLite database files or generated `-wal`/`-shm` artifacts.
4. Preserve snake_case naming and the existing unique indexes on file/source paths. Add indexes for new frequent relational filters rather than compensating with unbounded in-memory filtering.
5. Keep `DbInitializerHostedService` idempotent: migrations run first, then sqlite-vec and FTS5 virtual tables/triggers are ensured. Startup must work on a new writable `LORE_DATA_ROOT`.
6. Load sqlite-vec on every connection used for vector operations through `CreateVectorDbContextAsync`. Do not assume a normal EF SQLite context has the extension loaded.
7. Treat `float[384]` as a cross-component contract. Validate embedding length before writing, and coordinate model/table/reindex changes if it changes.
8. Preserve FTS external-content trigger semantics. Inserts, updates, and deletes of `file_chunks` must keep `file_chunks_fts` synchronized. If chunk replacement changes, verify that triggers do not create stale FTS rows.
9. Use parameterized SQL for virtual-table operations. JSON-serialize vectors as parameters, validate/bound limits, and do not interpolate user input.
10. Use `IDbContextFactory` for hosted services and parallel work. A registered singleton service must not capture a context with a shorter lifetime.
11. Use transactions around a file's vector replacement and final status update. A failed commit must not result in a searchable status.
12. Verify schema state with a temporary database and direct read-only inspection of `files`, `file_chunks`, `file_chunks_fts`, and `vec_file_chunks`. Check counts, delete behavior, and restart behavior.

## Example Usage / Pattern

```csharp
public static async Task EnsureVectorTablesCreatedAsync(
    this LoreDbContext dbContext,
    CancellationToken cancellationToken)
{
    const string sql = """
        CREATE VIRTUAL TABLE IF NOT EXISTS vec_file_chunks USING vec0(
            chunk_id INTEGER PRIMARY KEY,
            embedding float[384]
        );
        """;

    await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
}

await using LoreDbContext db = await factory.CreateVectorDbContextAsync(cancellationToken);
await using IDbContextTransaction transaction =
    await db.Database.BeginTransactionAsync(cancellationToken);

// Use provider parameters for vector JSON and chunk IDs in real writes.
await transaction.CommitAsync(cancellationToken);
```
