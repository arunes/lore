using System.Text.Json;
using System.Threading.Channels;
using Lore.Core.Pipeline;
using Lore.Data;
using Lore.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Lore.Core.Files;

public interface IFileSourceService
{
    Task<IReadOnlyList<FileSourceItem>> GetAsync(CancellationToken cancellationToken = default);

    Task<FileSourceItem> AddAsync(
        AddFileSourceRequest request,
        CancellationToken cancellationToken = default);

    Task<FileSourceItem> UpdateAsync(
        int id,
        UpdateFileSourceRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed record FileSourceItem(
    int Id,
    string Path,
    string? ExcludeExtensions,
    bool IsEnabled);

public sealed record AddFileSourceRequest(string Path, string? ExcludeExtensions);

public sealed record UpdateFileSourceRequest(string? ExcludeExtensions);

public sealed class FileSourceService(
    IDbContextFactory<LoreDbContext> dbContextFactory,
    Channel<FileArrivalRequest> fileArrivalChannel)
    : IFileSourceService
{
    public async Task<IReadOnlyList<FileSourceItem>> GetAsync(CancellationToken cancellationToken = default)
    {
        await using LoreDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        return await dbContext.FileSources
            .AsNoTracking()
            .OrderBy(source => source.Path)
            .Select(source => new FileSourceItem(
                source.Id,
                source.Path,
                source.ExcludeExtensions,
                source.IsEnabled))
            .ToListAsync(cancellationToken);
    }

    public async Task<FileSourceItem> AddAsync(
        AddFileSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        string path = NormalizePath(request.Path);
        string? excludeExtensions = NormalizeExtensions(request.ExcludeExtensions);

        await using LoreDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        bool exists = await dbContext.FileSources
            .AnyAsync(source => source.Path.ToLower() == path.ToLower(), cancellationToken);
        if (exists)
        {
            throw new ArgumentException("A file source with this path already exists.", nameof(request));
        }

        var source = new FileSource
        {
            Path = path,
            ExcludeExtensions = excludeExtensions,
            IsEnabled = true,
        };

        dbContext.FileSources.Add(source);
        await dbContext.SaveChangesAsync(cancellationToken);
        await ScanSourceAsync(source, cancellationToken);

        return new FileSourceItem(source.Id, source.Path, source.ExcludeExtensions, source.IsEnabled);
    }

    public async Task<FileSourceItem> UpdateAsync(
        int id,
        UpdateFileSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        await using LoreDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        FileSource source = await dbContext.FileSources
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"File source {id} was not found.");

        source.ExcludeExtensions = NormalizeExtensions(request.ExcludeExtensions);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new FileSourceItem(source.Id, source.Path, source.ExcludeExtensions, source.IsEnabled);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using LoreDbContext dbContext = await dbContextFactory.CreateVectorDbContextAsync(cancellationToken);
        FileSource source = await dbContext.FileSources
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw new KeyNotFoundException($"File source {id} was not found.");

        string sourcePath = Path.GetFullPath(source.Path)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string sourcePathForComparison = sourcePath.ToLowerInvariant();
        string sourcePathPrefix = sourcePathForComparison + Path.DirectorySeparatorChar;
        List<int> chunkIds = await dbContext.FileChunks
            .Where(chunk =>
                chunk.FileEntry.Path.ToLower() == sourcePathForComparison
                || chunk.FileEntry.Path.ToLower().StartsWith(sourcePathPrefix))
            .Select(chunk => chunk.Id)
            .ToListAsync(cancellationToken);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (int[] chunkBatch in chunkIds.Chunk(500))
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM vec_file_chunks WHERE chunk_id IN (SELECT value FROM json_each({0}));",
                [JsonSerializer.Serialize(chunkBatch)],
                cancellationToken);
            await dbContext.FileChunks
                .Where(chunk => chunkBatch.Contains(chunk.Id))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await dbContext.Files
            .Where(file =>
                file.Path.ToLower() == sourcePathForComparison
                || file.Path.ToLower().StartsWith(sourcePathPrefix))
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.FileSources
            .Where(item => item.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task ScanSourceAsync(FileSource source, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(source.Path))
        {
            return;
        }

        HashSet<string> excludedExtensions = (source.ExcludeExtensions ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string filePath in Directory.EnumerateFiles(source.Path, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (excludedExtensions.Contains(Path.GetExtension(filePath)))
            {
                continue;
            }

            await fileArrivalChannel.Writer.WriteAsync(
                new FileArrivalRequest(filePath),
                cancellationToken);
        }
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("A file source path is required.", nameof(path));
        }

        return Path.GetFullPath(path.Trim());
    }

    private static string? NormalizeExtensions(string? extensions)
    {
        string[] normalized = (extensions ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(extension => extension.StartsWith('.') ? extension : $".{extension}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return normalized.Length == 0 ? null : string.Join(',', normalized);
    }
}
