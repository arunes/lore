using Lore.Data.Models;

namespace Lore.Core.Files;

public interface IFileCatalogService
{
    Task<FileCatalogResponse> GetFilesAsync(
        FileCatalogQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record FileCatalogQuery(
    int Page = 1,
    int PageSize = 25,
    string? Search = null,
    string? Status = null,
    string? Extension = null,
    string? Category = null,
    string? DocumentType = null,
    string SortBy = "modified",
    string SortDirection = "desc");

public sealed record FileCatalogResponse(
    IReadOnlyList<FileCatalogItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    IReadOnlyDictionary<string, int> StatusCounts);

public sealed record FileCatalogItem(
    int Id,
    string Name,
    string Path,
    string Directory,
    string Extension,
    long Size,
    DateTime FileCreatedAt,
    DateTime FileModifiedAt,
    string ProcessStatus,
    string? Category,
    string? DocumentType);
