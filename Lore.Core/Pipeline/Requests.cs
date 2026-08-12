namespace Lore.Core.Pipeline;

public record FileArrivalRequest(string FilePath);

public record FileClassifyRequest(int FileId);

public record TextExtractRequest(string FilePath);

public record VectorizeRequest(int FileId);

public record ChunkingRequest(int FileId);