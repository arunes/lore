namespace Lore.Data.Models;

public class FileSource : BaseModel
{
    public int Id { get; set; }
    public required string Path { get; set; }
    public string? ExcludeExtensions { get; set; }
    public bool IsEnabled { get; set; }
}