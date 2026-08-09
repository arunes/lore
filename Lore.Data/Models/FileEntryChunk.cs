namespace Lore.Data.Models;

public class FileEntryChunk : BaseModel
{
    public int Id { get; set; }

    public int FileEntryId { get; set; }

    public int ChunkIndex { get; set; }

    public required string ChunkText { get; set; }

    public virtual FileEntry FileEntry { get; set; } = null!;
}