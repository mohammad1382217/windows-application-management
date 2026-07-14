using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Schedules;
using MilOps.Presentation.Common;
using MilOps.Presentation.Services;
using MilOps.Presentation.Views;

namespace MilOps.Presentation.ViewModels;

/// <summary>
/// Daily guard schedule (Lohe Posti): view by date, approve, and print the
/// paper-form-style schedule. For brevity this view models the read/approve/print
/// flow; full per-cell assignment editing uses a dedicated builder window.
/// </summary>
public sealed partial class SchedulesViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly IPrintService _print;

    public ObservableCollection<GuardAssignmentDto> Assignments { get; } = new();

    private DateTime _date = DateTime.Today;
    public DateTime Date
    {
        get => _date;
        set { _date = value; OnPropertyChanged(); }
    }

    private GuardScheduleDto? _current;
    public GuardScheduleDto? Current
    {
        get => _current;
        private set { _current = value; OnPropertyChanged(); ApproveCommand.NotifyCanExecuteChanged(); }
    }

    public SchedulesViewModel(ISender sender, IDialogService dialogs, IPrintService print)
    { _sender = sender; _dialogs = dialogs; _print = print; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var dto = await _sender.Send(new GetScheduleByDateQuery(DateOnly.FromDateTime(Date)));
            Current = dto;
            Assignments.Clear();
            if (dto is not null)
                foreach (var a in dto.Assignments) Assignments.Add(a);
        });
    }

    [RelayCommand]
    private void Create()
    {
        var builder = new ScheduleBuilderWindow(Date) { Owner = System.Windows.Application.Current.MainWindow };
        if (builder.ShowDialog() == true) _ = LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanApprove))]
    private async Task ApproveAsync()
    {
        if (Current is null) return;
        if (!_dialogs.Confirm("این برنامه برای چاپ تأیید شود؟ این عملیات در گزارش حسابرسی ثبت می‌شود.")) return;
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new ApproveScheduleCommand(Current.Id));
            if (!r.IsSuccess) _dialogs.Error(r.Error); else await LoadAsync();
        });
    }

    [RelayCommand]
    private void Print()
    {
        if (Current is null) { _dialogs.Warning("برای این تاریخ برنامه‌ای بارگذاری نشده است."); return; }
        try
        {
            PrintCore();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Print failed in SchedulesViewModel.");
            _dialogs.Error("چاپ انجام نشد. از اتصال و روشن بودن چاپگر اطمینان حاصل کنید.");
        }
    }

    private void PrintCore()
    {
        var doc = _print.BuildTableReport(
            "لوح پستی — برنامه نگهبانی روزانه",
            $"تاریخ: {PersianDate.ToJalali(Current.Date)} · وضعیت: {EnumText.Describe(Current.Status)}",
            new[] { "شناسه سرباز", "پست", "شیفت", "ساعت", "توضیح" },
            Assignments.Select(a => new[]
            {
                PersianDate.ToPersianDigits(a.SoldierId.ToString()),
                EnumText.Describe(a.Post), EnumText.Describe(a.Shift),
                a.ShiftStart is { } s && a.ShiftEnd is { } e
                    ? PersianDate.ToPersianDigits($"{s:HH:mm}–{e:HH:mm}") : "—",
                a.Note ?? "—"
            }));
        _print.Print(doc, "لوح پستی");
    }

    private bool CanApprove() =>
        Current is { Status: not Domain.Enums.ScheduleStatus.Approved };
}
