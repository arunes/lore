using System.ComponentModel.DataAnnotations;

namespace Lore.Data.Models;

public class Setting : BaseModel
{
    [Key]
    public required string Key { get; set; }
    public string? Value { get; set; }
}