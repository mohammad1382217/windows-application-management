using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Attendance;
using MilOps.Application.Soldiers;
using MilOps.Domain.Enums;
using MilOps.Presentation.Services;
using MilOps.Presentation.Views;

namespace MilOps.Presentation.ViewModels;

/// <summary>One roster row in the daily roll-call grid.</summary>
public sealed class AttendanceRowVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public int SoldierId { get; init; }
    public string PersonnelCode { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;

    private string _departmentName = string.Empty;
    public string DepartmentName { get => _departmentName; set { _departmentName = value; Notify(nameof(DepartmentName)); } }

    private AttendanceStatus? _status;
    public AttendanceStatus? Status { get => _status; set { _status = value; Notify(nameof(Status)); } }

    private string? _reason;
    public string? Reason { get => _reason; set { _reason = value; Notify(nameof(Reason)); } }
}

/// <summary>
/// Daily attendance roll-call: one row per active soldier for a chosen date,
/// bulk-editable status/reason, plus per-row quick actions (change department,
/// export full history report).
/// </summary>
public sealed partial class AttendanceViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly IPrintService _print;

    public ObservableCollection<AttendanceRowVm> Rows { get; } = new();
    public Array Statuses => Enum.GetValues(typeof(AttendanceStatus));

    private bool _isDirty;

    private DateTime _date = DateTime.Today;
    public DateTime Date
    {
        get => _date;
        set
        {
            if (_date == value) return;
            if (_isDirty && !_dialogs.Confirm("تغییرات ذخیره‌نشده از بین می‌رود. ادامه می‌دهید؟"))
            {
                OnPropertyChanged(nameof(Date));
                return;
            }
            _date = value; OnPropertyChanged();
            _ = LoadAsync();
        }
    }

    public AttendanceViewModel(ISender sender, IDialogService dialogs, IPrintService print)
    { _sender = sender; _dialogs = dialogs; _print = print; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var filter = new SoldierSearchRequest(null, null, true, null, 1, 500);
            var roster = await _sender.Send(new SearchSoldiersQuery(filter));

            var date = DateOnly.FromDateTime(Date);
            var existing = await _sender.Send(new ListAttendanceByDateQuery(date));
            var byId = existing.ToDictionary(a => a.SoldierId);

            Rows.Clear();
            foreach (var s in roster.Items.OrderBy(s => s.LastName))
            {
                var row = new AttendanceRowVm
                {
                    SoldierId = s.Id, PersonnelCode = s.PersonnelCode,
                    FirstName = s.FirstName, LastName = s.LastName, DepartmentName = s.DepartmentName
                };
                if (byId.TryGetValue(s.Id, out var a)) { row.Status = a.Status; row.Reason = a.Reason; }
                row.PropertyChanged += (_, _) => _isDirty = true;
                Rows.Add(row);
            }
            _isDirty = false;
        });
    }

    [RelayCommand]
    private async Task SaveAllAsync()
    {
        var toSave = Rows.Where(r => r.Status is not null).ToList();
        if (toSave.Count == 0) { _dialogs.Warning("هیچ وضعیتی برای ثبت انتخاب نشده است."); return; }

        await RunAsync(async () =>
        {
            var date = DateOnly.FromDateTime(Date);
            foreach (var row in toSave)
            {
                var r = await _sender.Send(new RecordAttendanceCommand(row.SoldierId, date, row.Status!.Value, row.Reason));
                if (!r.IsSuccess)
                {
                    ErrorMessage = $"{row.LastName} {row.FirstName}: {r.Error}";
                    return;
                }
            }
            _isDirty = false;
            _dialogs.Info($"وضعیت {toSave.Count} سرباز ثبت شد.");
        });
    }

    [RelayCommand]
    private void ChangeDepartment(AttendanceRowVm? row)
    {
        if (row is null) return;
        var dlg = new ChangeDepartmentDialog(row.SoldierId, $"{row.LastName} {row.FirstName}", row.DepartmentName)
        { Owner = System.Windows.Application.Current.MainWindow };
        if (dlg.ShowDialog() == true)
            row.DepartmentName = dlg.NewDepartmentValue;
    }

    [RelayCommand]
    private async Task ExportSoldierReportAsync(AttendanceRowVm? row)
    {
        if (row is null) return;
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new GetSoldierFullReportQuery(row.SoldierId, null, null));
            if (!r.IsSuccess || r.Value is null) { _dialogs.Error(r.Error ?? "گزارش یافت نشد."); return; }
            var doc = SoldierFullReportBuilder.Build(_print, r.Value);
            _print.ExportToPdf(doc, $"گزارش کامل {row.FirstName} {row.LastName}.pdf");
        });
    }
}
