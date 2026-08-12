namespace Lore.Core.TextExtractors;

[AttributeUsage(AttributeTargets.Class)]
public class SupportedExtensionsAttribute(params string[] extensions) : Attribute
{
    public string[] Extensions { get; } = extensions;
}