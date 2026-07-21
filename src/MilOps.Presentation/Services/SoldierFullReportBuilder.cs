using System.Windows.Documents;
using MilOps.Application.Soldiers;
using MilOps.Presentation.Common;

namespace MilOps.Presentation.Services;

/// <summary>
/// Builds the printable/PDF full-history report for one soldier: bio +
/// department history + guard-duty history + leave history + attendance
/// history. Shared by the Soldiers list and the Attendance roll-call screen.
/// </summary>
public static class SoldierFullReportBuilder
{
    public static FlowDocument Build(IPrintService print, SoldierFullReportDto dto)
    {
        var s = dto.Soldier;
        var rangeText = dto.From is { } f && dto.To is { } t
            ? $"بازه: {PersianDate.ToJalali(f)} تا {PersianDate.ToJalali(t)}"
            : "بازه: کل سوابق";

        var headerLines = new[]
        {
            $"نام و نام خانوادگی: {s.FirstName} {s.LastName}    درجه: {s.Rank}",
            $"کد پرسنلی: {PersianDate.ToPersianDigits(s.PersonnelCode)}    کد ملی: {PersianDate.ToPersianDigits(s.NationalCode)}",
            $"یگان/بخش فعلی: {s.DepartmentName}    وضعیت سلامت: {EnumText.Describe(s.HealthType)}    فعال: {(s.IsActive ? "بله" : "خیر")}",
            rangeText,
        };

        var sections = new (string, string[], IEnumerable<string[]>)[]
        {
            ("سابقه بخش", new[] { "بخش", "از تاریخ", "تا تاریخ" },
                dto.DepartmentHistory.Select(h => new[]
                {
                    h.DepartmentName,
                    PersianDate.ToJalali(h.EffectiveFrom),
                    h.EffectiveTo is { } to ? PersianDate.ToJalali(to) : "تاکنون"
                })),

            ("سابقه نگهبانی", new[] { "تاریخ", "پست", "شیفت" },
                dto.GuardAssignments.Select(g => new[]
                {
                    PersianDate.ToJalali(g.Date), EnumText.Describe(g.Post), EnumText.Describe(g.Shift)
                })),

            ("سابقه مرخصی", new[] { "از تاریخ", "تا تاریخ", "وضعیت", "علت" },
                dto.Leaves.Select(l => new[]
                {
                    PersianDate.ToJalali(l.StartDate), PersianDate.ToJalali(l.EndDate),
                    EnumText.Describe(l.Status), l.Reason
                })),

            ("سابقه حضور و غیاب", new[] { "تاریخ", "وضعیت", "توضیح" },
                dto.Attendance.Select(a => new[]
                {
                    PersianDate.ToJalali(a.Date), EnumText.Describe(a.Status), a.Reason ?? "—"
                })),
        };

        return print.BuildMultiSectionReport(
            $"گزارش کامل — {s.FirstName} {s.LastName}",
            string.Empty,
            headerLines,
            sections);
    }
}
