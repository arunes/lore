using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Lore.Data;

public static class DbContextExtensions
{
    public static async Task<LoreDbContext> CreateVectorDbContextAsync(
        this IDbContextFactory<LoreDbContext> dbContextFactory,
        CancellationToken cancellationToken = default
    )
    {
        var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var connection = dbContext.Database.GetDbConnection();

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        if (connection is SqliteConnection sqliteConn)
        {
            sqliteConn.LoadVector();
        }

        return dbContext;
    }

    public static async Task EnsureVectorTablesCreatedAsync(
        this LoreDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        var sqlFileContent = """
            CREATE VIRTUAL TABLE IF NOT EXISTS vec_file_chunks USING vec0(
                chunk_id INTEGER PRIMARY KEY,
                embedding float[384]
            );
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sqlFileContent, cancellationToken);
    }

    public static async Task EnsureFTSTablesCreatedAsync(
        this LoreDbContext dbContext,
        CancellationToken cancellationToken
    )
    {
        var sqlFtsSetup = """
            -- 1. Create FTS5 virtual table pointing to file_chunks
            CREATE VIRTUAL TABLE IF NOT EXISTS file_chunks_fts USING fts5(
                chunk_text,
                content='file_chunks',
                content_rowid='id'
            );

            -- 2. Trigger on INSERT
            CREATE TRIGGER IF NOT EXISTS file_chunks_ai AFTER INSERT ON file_chunks BEGIN
                INSERT INTO file_chunks_fts(rowid, chunk_text) VALUES (new.id, new.chunk_text);
            END;

            -- 3. Trigger on DELETE
            CREATE TRIGGER IF NOT EXISTS file_chunks_ad AFTER DELETE ON file_chunks BEGIN
                INSERT INTO file_chunks_fts(file_chunks_fts, rowid, chunk_text) VALUES('delete', old.id, old.chunk_text);
            END;

            -- 4. Trigger on UPDATE
            CREATE TRIGGER IF NOT EXISTS file_chunks_au AFTER UPDATE ON file_chunks BEGIN
                INSERT INTO file_chunks_fts(file_chunks_fts, rowid, chunk_text) VALUES('delete', old.id, old.chunk_text);
                INSERT INTO file_chunks_fts(rowid, chunk_text) VALUES (new.id, new.chunk_text);
            END;
        """;

        await dbContext.Database.ExecuteSqlRawAsync(sqlFtsSetup, cancellationToken);
    }
}
