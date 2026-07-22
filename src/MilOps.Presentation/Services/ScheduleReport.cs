using System.IO;
using System.Windows.Documents;
using MilOps.Application.Schedules;
using MilOps.Presentation.Common;

namespace MilOps.Presentation.Services;

/// <summary>
/// Builds the printable "لوح پستی" report for a schedule. Shared by the
/// preview modal and any other spot that prints or exports a schedule.
/// </summary>
public static class ScheduleReport
{
    public static FlowDocument Build(IPrintService print, GuardScheduleDto dto)
    {
        return print.BuildTableReport(
            "لوح پستی — برنامه نگهبانی روزانه",
            $"تاریخ: {PersianDate.ToJalali(dto.Date)} · تعداد نفرات: {PersianDate.ToPersianDigits(dto.Assignments.Count.ToString())}",
            new[] { "سرباز", "پست", "شیفت", "ساعت", "توضیح" },
            dto.Assignments.Select(a => new[]
            {
                a.SoldierName ?? PersianDate.ToPersianDigits(a.SoldierId.ToString()),
                EnumText.Describe(a.Post), EnumText.Describe(a.Shift),
                a.ShiftStart is { } s && a.ShiftEnd is { } e
                    ? PersianDate.ToPersianDigits($"{s:HH:mm}–{e:HH:mm}") : "—",
                a.Note ?? "—"
            }));
    }

    /// <summary>
    /// Silently exports the schedule PDF into the app's configured export
    /// folder (or a MilOps subfolder under Documents if none is set) with no
    /// save dialog — used right after "ثبت نهایی" so finalizing a schedule
    /// always leaves a PDF behind without an extra manual step. Returns the
    /// full path written.
    /// </summary>
    public static string ExportToDefaultFolder(IPrintService print, IAppSettingsStore settings, GuardScheduleDto dto)
    {
        var folder = settings.Load().ExportFolder;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MilOps");
        Directory.CreateDirectory(folder);

        var fileName = $"لوح پستی {PersianDate.ToJalali(dto.Date)}.pdf".Replace('/', '-');
        var path = Path.Combine(folder, fileName);
        print.ExportToPdfFile(Build(print, dto), path);
        return path;
    }
}
