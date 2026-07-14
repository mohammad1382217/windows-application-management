using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using MilOps.Presentation.Common;

namespace MilOps.Presentation.Converters;

/// <summary>
/// Two-way converter between a Gregorian date (<see cref="DateOnly"/>,
/// <see cref="DateTime"/>, or their nullable forms) and a Jalali "yyyy/MM/dd"
/// string for display and text-box entry. Use with TextBox.Text bindings
/// (UpdateSourceTrigger=LostFocus) and DataGrid date columns.
/// </summary>
public sealed class JalaliDateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        DateOnly d => PersianDate.ToJalali(d),
        DateTime dt => PersianDate.ToJalali(dt),
        null => string.Empty,
        _ => string.Empty
    };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string;
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        var nullable = Nullable.GetUnderlyingType(targetType) is not null || !targetType.IsValueType;

        if (string.IsNullOrWhiteSpace(text))
            return nullable ? null : DependencyProperty.UnsetValue;

        if (!PersianDate.TryParse(text, out var date))
            return DependencyProperty.UnsetValue; // invalid -> reject the edit

        if (underlying == typeof(DateOnly)) return date;
        if (underlying == typeof(DateTime)) return date.ToDateTime(TimeOnly.MinValue);
        return DependencyProperty.UnsetValue;
    }
}

/// <summary>Jalali date + time (for audit/created timestamps). Display-only.</summary>
public sealed class JalaliDateTimeConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt ? PersianDate.ToJalaliDateTime(dt) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Renders Latin digits in any value as Persian digits (display-only).</summary>
public sealed class PersianDigitsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is null ? string.Empty : PersianDate.ToPersianDigits(value.ToString() ?? string.Empty);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s ? PersianDate.ToLatinDigits(s) : value!;
}

/// <summary>Null or empty string -> Visibility.Collapsed; otherwise Visible.</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>
/// Object/string null -> Visibility, honoring an optional "inverse" parameter.
/// Default: non-null/non-empty => Visible. With ConverterParameter=inverse:
/// null/empty => Visible (e.g. show a placeholder when nothing is selected).
/// Unlike <see cref="NullToCollapsedConverter"/> this works for ANY object,
/// not just strings.
/// </summary>
public sealed class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var isEmpty = value is null || (value is string s && s.Length == 0);
        var inverse = string.Equals(parameter as string, "inverse", StringComparison.OrdinalIgnoreCase);
        var visible = inverse ? isEmpty : !isEmpty;
        return visible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>bool -> Visibility (true=Visible, false=Collapsed).</summary>
public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>Visible when the bound bool is FALSE; collapsed when true.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => (value is bool b && b) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is Visibility v && v != Visibility.Visible;
}

/// <summary>
/// Converts an enum value to its Persian [Description] attribute.
/// Falls back to the raw enum name if no attribute is set.
/// </summary>
public sealed class EnumDescriptionConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        var field = value.GetType().GetField(value.ToString()!);
        var attr = field?.GetCustomAttribute<DescriptionAttribute>();
        return attr?.Description ?? value.ToString() ?? string.Empty;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Inverted bool. Handy for IsNotBusy bindings.</summary>
public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is bool b && b);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => !(value is bool b && b);
}
