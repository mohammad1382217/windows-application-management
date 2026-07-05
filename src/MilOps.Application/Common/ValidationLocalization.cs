using System.Globalization;
using FluentValidation;

namespace MilOps.Application.Common;

/// <summary>
/// Configures FluentValidation for Persian output:
///   - Message templates come from FluentValidation's built-in fa translations
///     ("نباید خالی باشد" و غیره).
///   - Property names inside those messages resolve through the dictionary
///     below so users see "نام کاربری" instead of "Username".
/// Call once at startup (idempotent).
/// </summary>
public static class ValidationLocalization
{
    private static readonly Dictionary<string, string> FieldNamesFa = new()
    {
        ["Username"] = "نام کاربری",
        ["Password"] = "گذرواژه",
        ["FullName"] = "نام کامل",
        ["FirstName"] = "نام",
        ["LastName"] = "نام خانوادگی",
        ["FatherName"] = "نام پدر",
        ["NationalCode"] = "کد ملی",
        ["PersonnelCode"] = "کد پرسنلی",
        ["Rank"] = "درجه",
        ["DepartmentName"] = "یگان/بخش",
        ["ServiceStartDate"] = "تاریخ شروع خدمت",
        ["ServiceEndDate"] = "تاریخ پایان خدمت",
        ["EntryDate"] = "تاریخ ورود",
        ["StartDate"] = "تاریخ شروع",
        ["EndDate"] = "تاریخ پایان",
        ["Reason"] = "علت",
        ["ValidDays"] = "مدت اعتبار (روز)",
        ["SoldierId"] = "شناسه سرباز",
        ["WeaponNumber"] = "شماره سلاح",
        ["Model"] = "مدل",
        ["Assignments"] = "تخصیص‌ها",
        ["Id"] = "شناسه",
        ["Date"] = "تاریخ",
        ["Purpose"] = "هدف مجوز",
        ["Role"] = "نقش",
    };

    public static void Apply()
    {
        ValidatorOptions.Global.LanguageManager.Enabled = true;
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("fa");
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
            member is not null && FieldNamesFa.TryGetValue(member.Name, out var fa)
                ? fa
                : member?.Name;
    }
}
