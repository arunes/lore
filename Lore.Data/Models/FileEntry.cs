namespace Lore.Data.Models;

public enum FileProcessStatus
{
    Pending,
    TextExtracted,
    Classified,
    ChunksCreated,
    Done,

    NotSupportedFile,
    EmptyContent,

    TextExtractionFailed,
    ClassificationFailed,
    VectorizationFailed,
}

public class FileEntry : BaseModel
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Path { get; set; }

    public required string Directory { get; set; }

    public required string Extension { get; set; }

    public string? Content { get; set; }

    public int? PrimaryCategoryId { get; set; }

    public int? DocumentTypeId { get; set; }

    public DateTime FileCreatedAt { get; set; }

    public DateTime FileModifiedAt { get; set; }

    public long Size { get; set; }

    public required string Hash { get; set; }

    public required FileProcessStatus ProcessStatus { get; set; }

    public virtual ICollection<FileEntryChunk> Chunks { get; set; } = [];
    public virtual PrimaryCategory? PrimaryCategory { get; set; }
    public virtual DocumentType? DocumentType { get; set; }
}
