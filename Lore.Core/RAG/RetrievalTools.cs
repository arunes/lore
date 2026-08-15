using System.ComponentModel;

using Lore.Common.Models;
using Lore.Data;
using Lore.Core.Retrieval;

using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Lore.Core.Settings;
using ModelContextProtocol.Server;

namespace Lore.Core.RAG;

[McpServerToolType]
public class RetrievalTools(
    IRetrievalService searchTools,
    LoreDbContext dbContext,
    IUserSettingsService userSettings)
{
    public record SearchFilesByNameResponse(
        int Id,
        string Name,
        string Directory,
        string? Category,
        string? DocumentType);

    public record DirectoryListingItem(
        int? FileId,
        string Name,
        string Path,
        bool IsDirectory,
        string? Extension,
        long? SizeBytes);

    public record FileMetadataDto(
        int Id,
        string Name,
        string Path,
        string? Category,
        string? DocumentType,
        DateTime? ModifiedAt);

    [KernelFunction]
    [McpServerTool]
    [Description("Searches the contents of indexed files by topic, keywords, or natural language query. Use this to find relevant files or documents when you don't know the exact file name.")]
    public async Task<List<DocumentChunkFile>> SearchFileContentsAsync(
        [Description("The search query terms or natural language topic.")] RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        if (!userSettings.GetSetting<bool>(UserSettingsType.ToolsSearchFileContents))
        {
            throw new Exception("Tool is disabled. User can enable the `Search File Contents` tool in settings.");
        }

        var chunkIds = await searchTools.RetrieveDocumentChunksAsync(query, cancellationToken);
        return await searchTools.GetChunkContentsAsync(chunkIds, cancellationToken);
    }

    [KernelFunction]
    [McpServerTool]
    [Description("Finds file paths by matching text in the file name or folder path string. Use when the user gives a specific file name or extension.")]
    public async Task<List<SearchFilesByNameResponse>> SearchFilesByNameAsync(
        [Description("The exact or partial file name (e.g., 'report.pdf', 'budget').")] string fileName,
        CancellationToken cancellationToken = default)
    {
        if (!userSettings.GetSetting<bool>(UserSettingsType.ToolsSearchFilesByName))
        {
            throw new Exception("Tool is disabled. User can enable the `Search Files by Name` tool in settings.");
        }

        return await dbContext
            .Files
            .AsNoTracking()
            .Where(fl => fl.Path.ToLower().Contains(fileName.Trim().ToLowerInvariant()))
            .Select(fl => new SearchFilesByNameResponse(
                fl.Id,
                fl.Name,
                fl.Directory,
                fl.PrimaryCategory != null ? fl.PrimaryCategory.Name : null,
                fl.DocumentType != null ? fl.DocumentType.Name : null))
            .ToListAsync(cancellationToken);
    }

    [KernelFunction]
    [McpServerTool]
    [Description("Retrieves the full text content of a file located at the specified file path.")]
    public async Task<string> GetFullFileContentAsync(
        [Description("The exact path of the file to read.")] string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!userSettings.GetSetting<bool>(UserSettingsType.ToolsGetFullFileContent))
        {
            throw new Exception("Tool is disabled. User can enable the `Get Full File Content` tool in settings.");
        }

        string normalizedFilePath = Path.GetFullPath(filePath);
        string normalizedFilePathForComparison = normalizedFilePath.ToLowerInvariant();
        var sourcePaths = await dbContext
            .FileSources
            .AsNoTracking()
            .Where(fs => fs.IsEnabled)
            .Select(fs => fs.Path)
            .ToListAsync(cancellationToken);

        var hasPermission = sourcePaths.Any(sourcePath =>
        {
            string normalizedSourcePath = Path.GetFullPath(sourcePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedSourcePathForComparison = normalizedSourcePath.ToLowerInvariant();

            return normalizedFilePathForComparison == normalizedSourcePathForComparison
                || normalizedFilePathForComparison.StartsWith(
                    normalizedSourcePathForComparison + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal);
        });

        if (!hasPermission)
        {
            return "You do not have permissions to retrieve the content of this file.";
        }

        if (!File.Exists(filePath))
        {
            return "File does not exist.";
        }

        var dbFileContent = await dbContext
            .Files
            .Where(fl => fl.Path.ToLower() == normalizedFilePathForComparison)
            .Select(fl => fl.Content)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (dbFileContent == null || string.IsNullOrWhiteSpace(dbFileContent))
        {
            return "File is not indexed or file content is empty.";
        }

        return dbFileContent;
    }

    [KernelFunction]
    [McpServerTool]
    [Description("Lists files and subdirectories within a given folder path. Use this to inspect what files or sub-folders exist inside a directory.")]
    public async Task<List<DirectoryListingItem>> GetDirectoryContentsAsync(
        [Description("The absolute or relative directory path to list (e.g., 'C:\\Docs\\Finance' or '/projects/alpha').")] string folderPath,
        CancellationToken cancellationToken = default)
    {
        if (!userSettings.GetSetting<bool>(UserSettingsType.ToolsGetDirectoryContents))
        {
            throw new Exception("Tool is disabled. User can enable the `Get Directory Contents` tool in settings.");
        }

        string normalizedFolderPath = Path.GetFullPath(folderPath);
        string normalizedFolderPathForComparison = normalizedFolderPath.ToLowerInvariant();
        var sourcePaths = await dbContext.FileSources
            .AsNoTracking()
            .Where(fs => fs.IsEnabled)
            .Select(fs => fs.Path)
            .ToListAsync(cancellationToken);

        var isAllowed = sourcePaths.Any(sourcePath =>
        {
            string normalizedSourcePath = Path.GetFullPath(sourcePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedSourcePathForComparison = normalizedSourcePath.ToLowerInvariant();

            return normalizedFolderPathForComparison == normalizedSourcePathForComparison
                || normalizedFolderPathForComparison.StartsWith(
                    normalizedSourcePathForComparison + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal);
        });

        if (!isAllowed)
        {
            return [];
        }

        return await dbContext.Files
            .AsNoTracking()
            .Where(f => f.Directory.ToLower() == normalizedFolderPathForComparison)
            .Select(f => new DirectoryListingItem(
                f.Id, f.Name, f.Path, false, f.Extension, f.Size))
            .ToListAsync(cancellationToken);
    }

    [KernelFunction]
    [McpServerTool]
    [Description("Finds directory paths matching a given folder name or keyword. Use when the user refers to a specific folder or location.")]
    public async Task<List<string>> SearchDirectoriesByNameAsync(
        [Description("The partial or exact name of the directory/folder (e.g., 'Invoices', '2024').")] string directoryKeyword,
        CancellationToken cancellationToken = default)
    {
        if (!userSettings.GetSetting<bool>(UserSettingsType.ToolsSearchDirectoriesByName))
        {
            throw new Exception("Tool is disabled. User can enable the `Tools Search Directories by Name` tool in settings.");
        }

        return await dbContext.Files
            .AsNoTracking()
            .Where(f => f.Directory.ToLower().Contains(directoryKeyword.Trim().ToLowerInvariant()))
            .Select(f => f.Directory)
            .Distinct()
            .Take(20)
            .ToListAsync(cancellationToken);
    }

    [KernelFunction]
    [McpServerTool]
    [Description("Filters files by category, document type, file extension, or date range. Use for structural or metadata queries.")]
    public async Task<List<FileMetadataDto>> GetFilesByMetadataAsync(
        [Description("Optional category filter.")] string? categoryName = null,
        [Description("Optional document type filter.")] string? documentTypeName = null,
        [Description("Optional extension filter (e.g. '.pdf', '.docx').")] string? extension = null,
        [Description("Limit maximum results returned.")] int limit = 20,
        CancellationToken cancellationToken = default)
    {
        if (!userSettings.GetSetting<bool>(UserSettingsType.ToolsGetFilesByMetadata))
        {
            throw new Exception("Tool is disabled. User can enable the `Get Files by Metadata` tool in settings.");
        }

        var query = dbContext.Files.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(categoryName))
        {
            string normalizedCategoryName = categoryName.Trim().ToLowerInvariant();
            query = query.Where(f =>
                f.PrimaryCategory != null
                && f.PrimaryCategory.Name.ToLower().Contains(normalizedCategoryName));
        }

        if (!string.IsNullOrWhiteSpace(documentTypeName))
        {
            string normalizedDocumentTypeName = documentTypeName.Trim().ToLowerInvariant();
            query = query.Where(f =>
                f.DocumentType != null
                && f.DocumentType.Name.ToLower().Contains(normalizedDocumentTypeName));
        }

        if (!string.IsNullOrWhiteSpace(extension))
        {
            string normalizedExtension = extension.Trim().ToLowerInvariant();
            query = query.Where(f => f.Extension.ToLower() == normalizedExtension);
        }

        return await query
            .Take(limit)
            .Select(f => new FileMetadataDto(
                f.Id,
                f.Name,
                f.Path,
                f.PrimaryCategory != null ? f.PrimaryCategory.Name : null,
                f.DocumentType != null ? f.DocumentType.Name : null,
                f.FileModifiedAt))
            .ToListAsync(cancellationToken);
    }

    [KernelFunction]
    [McpServerTool]
    [Description("Retrieves all valid categories and document types available in the system. Use before filtering files by metadata.")]
    public async Task<object> ListAvailableCategoriesAndTypesAsync(CancellationToken cancellationToken = default)
    {
        if (!userSettings.GetSetting<bool>(UserSettingsType.ToolsListAvailableCategoriesAndTypes))
        {
            throw new Exception("Tool is disabled. User can enable the `List Available Categories and Types` tool in settings.");
        }

        var categories = await dbContext.PrimaryCategories.AsNoTracking().Select(c => c.Name).ToListAsync(cancellationToken);
        var docTypes = await dbContext.DocumentTypes.AsNoTracking().Select(dt => dt.Name).ToListAsync(cancellationToken);

        return new { Categories = categories, DocumentTypes = docTypes };
    }
}
