namespace Lore.Core.Pipeline;

public record FileArrivalRequest(string FilePath, string? TraceParent = null);

public record FileClassifyRequest(int FileId, string? TraceParent = null);

public record TextExtractRequest(string FilePath, string? TraceParent = null);

public record VectorizeRequest(int FileId, string? TraceParent = null);

public record ChunkingRequest(int FileId, string? TraceParent = null);