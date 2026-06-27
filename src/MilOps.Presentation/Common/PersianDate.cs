using System.Globalization;

namespace MilOps.Presentation.Common;

/// <summary>
/// Converts between Gregorian (Miladi) dates — how the domain/database stores
/// them — and Jalali (Shamsi) strings shown to the user. Storage stays
/// Gregorian; only the presentation layer speaks Jalali.
///
/// Canonical text form is "yyyy/MM/dd" with Persian (Farsi) digits, e.g.
/// ۱۴۰۳/۰۳/۰۷. Parsing accepts both Persian and Latin digits and the common
/// separators '/', '-' and '.'.
/// </summary>
public static class PersianDate
{
    private static readonly PersianCalendar Cal = new();

    // Persian month names (for long/headline formatting when needed).
    public static readonly string[] MonthNames =
    {
        "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    };

    public static readonly string[] WeekDayNames =
    {
        "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه"
    };

    /// <summary>Jalali "yyyy/MM/dd" (Persian digits) for a <see cref="DateOnly"/>.</summary>
    public static string ToJalali(DateOnly date) => ToJalali(date.ToDateTime(TimeOnly.MinValue));

    /// <summary>Jalali "yyyy/MM/dd" (Persian digits) for a <see cref="DateTime"/>.</summary>
    public static string ToJalali(DateTime date)
    {
        var y = Cal.GetYear(date);
        var m = Cal.GetMonth(date);
        var d = Cal.GetDayOfMonth(date);
        return ToPersianDigits($"{y:0000}/{m:00}/{d:00}");
    }

    /// <summary>Jalali date + 24h time, e.g. ۱۴۰۳/۰۳/۰۷ ۱۴:۳۰ (for audit/timestamps).</summary>
    public static string ToJalaliDateTime(DateTime date)
    {
        var local = date.Kind == DateTimeKind.Utc ? date.ToLocalTime() : date;
        return ToPersianDigits($"{ToJalali(local)} {local:HH:mm}");
    }

    /// <summary>Long human form, e.g. ۷ خرداد ۱۴۰۳.</summary>
    public static string ToJalaliLong(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        var m = Cal.GetMonth(dt);
        return ToPersianDigits($"{Cal.GetDayOfMonth(dt)} {MonthNames[m - 1]} {Cal.GetYear(dt)}");
    }

    /// <summary>Today's date in the Jalali calendar as a <see cref="DateOnly"/> (Gregorian backing).</summary>
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    /// <summary>
    /// Parse a Jalali date string ("1403/03/07", Persian or Latin digits,
    /// '/'/'-'/'.'-separated) into a Gregorian <see cref="DateOnly"/>.
    /// </summary>
    public static bool TryParse(string? text, out DateOnly date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var normalized = ToLatinDigits(text.Trim()).Replace('-', '/').Replace('.', '/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return false;

        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var jy) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var jm) ||
            !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var jd))
            return false;

        // Two-digit year convenience: 03 -> 1403.
        if (jy < 100) jy += 1400;
        if (jm is < 1 or > 12) return false;
        if (jd < 1 || jd > Cal.GetDaysInMonth(jy, jm)) return false;

        try
        {
            var g = Cal.ToDateTime(jy, jm, jd, 0, 0, 0, 0);
            date = DateOnly.FromDateTime(g);
            return true;
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    public static string ToPersianDigits(string s)
    {
        var c = s.ToCharArray();
        for (var i = 0; i < c.Length; i++)
            if (c[i] is >= '0' and <= '9')
                c[i] = (char)('۰' + (c[i] - '0'));
        return new string(c);
    }

    public static string ToLatinDigits(string s)
    {
        var c = s.ToCharArray();
        for (var i = 0; i < c.Length; i++)
        {
            // Persian ۰..۹ (U+06F0) and Arabic-Indic ٠..٩ (U+0660).
            if (c[i] is >= '۰' and <= '۹') c[i] = (char)('0' + (c[i] - '۰'));
            else if (c[i] is >= '٠' and <= '٩') c[i] = (char)('0' + (c[i] - '٠'));
        }
        return new string(c);
    }
}
