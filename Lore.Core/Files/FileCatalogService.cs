using Lore.Data;
using Lore.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Lore.Core.Files;

public sealed class FileCatalogService(IDbContextFactory<LoreDbContext> dbContextFactory)
    : IFileCatalogService
{
    private static readonly string[] SortableColumns =
    [
        "name",
        "path",
        "extension",
        "size",
        "modified",
        "status",
        "category",
        "documenttype"
    ];

    public async Task<FileCatalogResponse> GetFilesAsync(
        FileCatalogQuery request,
        CancellationToken cancellationToken = default)
    {
        int page = Math.Max(request.Page, 1);
        int pageSize = Math.Clamp(request.PageSize, 1, 100);
        string search = request.Search?.Trim().ToLowerInvariant() ?? string.Empty;
        string extension = request.Extension?.Trim().ToLowerInvariant() ?? string.Empty;
        string category = request.Category?.Trim().ToLowerInvariant() ?? string.Empty;
        string documentType = request.DocumentType?.Trim().ToLowerInvariant() ?? string.Empty;

        await using LoreDbContext dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        Dictionary<FileProcessStatus, int> statusCounts = await dbContext.Files
            .AsNoTracking()
            .GroupBy(file => file.ProcessStatus)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.Status, item => item.Count, cancellationToken);

        IQueryable<FileEntry> files = dbContext.Files
            .AsNoTracking()
            .Where(file =>
                string.IsNullOrEmpty(search)
                || file.Name.ToLower().Contains(search)
                || file.Path.ToLower().Contains(search)
                || file.Directory.ToLower().Contains(search))
            .Where(file => string.IsNullOrEmpty(extension) || file.Extension.ToLower() == extension)
            .Where(file =>
                string.IsNullOrEmpty(category)
                || (file.PrimaryCategory != null && file.PrimaryCategory.Name.ToLower().Contains(category)))
            .Where(file =>
                string.IsNullOrEmpty(documentType)
                || (file.DocumentType != null && file.DocumentType.Name.ToLower().Contains(documentType)));

        if (!string.IsNullOrWhiteSpace(request.Status)
            && Enum.TryParse<FileProcessStatus>(request.Status, ignoreCase: true, out FileProcessStatus status))
        {
            files = files.Where(file => file.ProcessStatus == status);
        }

        string sortBy = request.SortBy.Trim().ToLowerInvariant();
        if (!SortableColumns.Contains(sortBy, StringComparer.Ordinal))
        {
            sortBy = "modified";
        }

        bool descending = !string.Equals(request.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        files = ApplySort(files, sortBy, descending);

        int totalCount = await files.CountAsync(cancellationToken);
        int totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)pageSize);
        page = totalPages == 0 ? 1 : Math.Min(page, totalPages);

        List<FileCatalogItem> items = await files
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(file => new FileCatalogItem(
                file.Id,
                file.Name,
                file.Path,
                file.Directory,
                file.Extension,
                file.Size,
                file.FileCreatedAt,
                file.FileModifiedAt,
                file.ProcessStatus.ToString(),
                file.PrimaryCategory == null ? null : file.PrimaryCategory.Name,
                file.DocumentType == null ? null : file.DocumentType.Name))
            .ToListAsync(cancellationToken);

        IReadOnlyDictionary<string, int> allStatusCounts = Enum.GetValues<FileProcessStatus>()
            .ToDictionary(status => status.ToString(), status => statusCounts.GetValueOrDefault(status));

        return new FileCatalogResponse(
            items,
            page,
            pageSize,
            totalCount,
            totalPages,
            allStatusCounts);
    }

    private static IQueryable<FileEntry> ApplySort(
        IQueryable<FileEntry> files,
        string sortBy,
        bool descending)
    {
        return (sortBy, descending) switch
        {
            ("name", false) => files.OrderBy(file => file.Name).ThenBy(file => file.Id),
            ("name", true) => files.OrderByDescending(file => file.Name).ThenByDescending(file => file.Id),
            ("path", false) => files.OrderBy(file => file.Path).ThenBy(file => file.Id),
            ("path", true) => files.OrderByDescending(file => file.Path).ThenByDescending(file => file.Id),
            ("extension", false) => files.OrderBy(file => file.Extension).ThenBy(file => file.Id),
            ("extension", true) => files.OrderByDescending(file => file.Extension).ThenByDescending(file => file.Id),
            ("size", false) => files.OrderBy(file => file.Size).ThenBy(file => file.Id),
            ("size", true) => files.OrderByDescending(file => file.Size).ThenByDescending(file => file.Id),
            ("status", false) => files.OrderBy(file => file.ProcessStatus).ThenBy(file => file.Id),
            ("status", true) => files.OrderByDescending(file => file.ProcessStatus).ThenByDescending(file => file.Id),
            ("category", false) => files.OrderBy(file => file.PrimaryCategory!.Name).ThenBy(file => file.Id),
            ("category", true) => files.OrderByDescending(file => file.PrimaryCategory!.Name).ThenByDescending(file => file.Id),
            ("documenttype", false) => files.OrderBy(file => file.DocumentType!.Name).ThenBy(file => file.Id),
            ("documenttype", true) => files.OrderByDescending(file => file.DocumentType!.Name).ThenByDescending(file => file.Id),
            ("modified", false) => files.OrderBy(file => file.FileModifiedAt).ThenBy(file => file.Id),
            _ => files.OrderByDescending(file => file.FileModifiedAt).ThenByDescending(file => file.Id),
        };
    }
}
