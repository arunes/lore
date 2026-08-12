using System.ComponentModel;
using Lore.Common.Models;
using Lore.Data;
using Lore.Core.Retrieval;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace Lore.Core.RAG;

public class KernelRetrievalTools(IRetrievalService searchTools, LoreDbContext dbContext)
{
    public record SearchFilesByNameResponse(
        int Id,
        string Name,
        string Directory,
        string? Category,
        string? DocumentType);

    [KernelFunction]
    [Description("Searches the contents of indexed files by topic, keywords, or natural language query. Use this to find relevant files or documents when you don't know the exact file name.")]
    public async Task<List<DocumentChunkFile>> SearchFileContentsAsync(
        [Description("The search query terms or natural language topic.")] RetrievalQuery query,
        CancellationToken cancellationToken = default)
    {
        var chunkIds = await searchTools.RetrieveDocumentChunksAsync(query, cancellationToken);
        return await searchTools.GetChunkContentsAsync(chunkIds, cancellationToken);
    }

    [KernelFunction]
    [Description("Finds file paths by matching text in the file name or folder path string. Use when the user gives a specific file name or extension.")]
    public async Task<List<SearchFilesByNameResponse>> SearchFilesByNameAsync(
        [Description("The exact or partial file name (e.g., 'report.pdf', 'budget').")] string fileName,
        CancellationToken cancellationToken = default)
    {
        return await dbContext
            .Files
            .AsNoTracking()
            .Where(fl => fl.Path.Contains(fileName))
            .Select(fl => new SearchFilesByNameResponse(
                fl.Id, 
                fl.Name, 
                fl.Directory, 
                fl.PrimaryCategory != null ? fl.PrimaryCategory.Name : null, 
                fl.DocumentType != null ? fl.DocumentType.Name : null))
            .ToListAsync(cancellationToken);
    }

    [KernelFunction]
    [Description("Retrieves the full text content of a file located at the specified file path.")]
    public async Task<string> GetFullFileContentAsync(
        [Description("The exact path of the file to read.")] string filePath,
        CancellationToken cancellationToken = default)
    {
        var hasPermission = await dbContext
            .FileSources
            .AsNoTracking()
            .Where(fs => fs.IsEnabled && filePath.StartsWith(fs.Path))
            .AnyAsync(cancellationToken);

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
            .Where(fl => fl.Path == filePath)
            .Select(fl => fl.Content)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        if (dbFileContent == null || string.IsNullOrWhiteSpace(dbFileContent))
        {
            return "File is not indexed or file content is empty.";
        }

        return dbFileContent;
    }
}