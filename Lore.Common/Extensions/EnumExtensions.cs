using System.Reflection;

namespace Lore.Common.Extensions;

public static class EnumExtensions
{
    public static TAttribute? GetAttribute<TAttribute>(this Enum value)
        where TAttribute : Attribute
    {
        Type type = value.GetType();
        string? name = Enum.GetName(type, value);

        if (name == null)
        {
            return null;
        }

        return type.GetField(name)?.GetCustomAttribute<TAttribute>();
    }
}
