using Lore.Common.Models;

namespace Lore.Core.Retrieval;

public record DocumentChunkFile(
    int Id,
    string Path,
    string? CategoryName,
    string? DocTypeName,
    List<DocumentChunk> Chunks);

public record DocumentChunk(int Id, string ChunkText, int ChunkIndex);

public interface IRetrievalService
{
    Task<List<DocumentChunkFile>> GetChunkContentsAsync(
        List<int> documentChunkIds,
        CancellationToken cancellationToken
    );

    Task<List<int>> RetrieveDocumentChunksAsync(
        RetrievalQuery query,
        CancellationToken cancellationToken
    );
}