using System.ComponentModel;
using System.Reflection;

namespace MilOps.Application.Common;

/// <summary>
/// Persian display text for enum values, read from their [Description]
/// attribute. Used to keep audit-log detail strings in Persian instead of
/// leaking raw enum names or requiring a Presentation-layer dependency.
/// (Mirrors MilOps.Presentation.Common.EnumText, which the Application layer
/// cannot reference — Presentation depends on Application, not vice versa.)
/// </summary>
public static class EnumDescriptions
{
    public static string Describe<T>(T value) where T : struct, Enum
    {
        var field = typeof(T).GetField(value.ToString());
        var attr = field?.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? value.ToString();
    }
}
