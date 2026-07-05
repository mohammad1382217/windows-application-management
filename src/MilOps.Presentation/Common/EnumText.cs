using System.ComponentModel;
using System.Reflection;

namespace MilOps.Presentation.Common;

/// <summary>
/// Persian display text for enum values, read from their [Description]
/// attributes (same source the EnumDescriptionConverter uses in XAML).
/// For use in code paths such as printed reports.
/// </summary>
public static class EnumText
{
    public static string Describe<T>(T value) where T : struct, Enum
    {
        var field = typeof(T).GetField(value.ToString());
        var attr = field?.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? value.ToString();
    }

    public static string Describe<T>(T? value, string fallback = "—") where T : struct, Enum
        => value.HasValue ? Describe(value.Value) : fallback;
}
