namespace Lore.Data.Models;

public class DocumentType : BaseModel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Keywords { get; set; }
    public virtual ICollection<FileEntry> FileEntries { get; set; } = [];
}