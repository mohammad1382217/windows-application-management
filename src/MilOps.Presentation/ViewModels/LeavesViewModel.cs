using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Leaves;
using MilOps.Application.Soldiers;
using MilOps.Domain.Enums;
using MilOps.Presentation.Common;
using MilOps.Presentation.Services;
using MilOps.Presentation.Views;

namespace MilOps.Presentation.ViewModels;

/// <summary>Leave management: request, approve/reject, print leave records.</summary>
public sealed partial class LeavesViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly IPrintService _print;

    public ObservableCollection<LeaveDto> Items { get; } = new();

    /// <summary>Active soldiers offered by the new-request picker.</summary>
    public ObservableCollection<SoldierDto> Soldiers { get; } = new();

    // Picked from the soldier list — no more free-typed IDs.
    public SoldierDto? NewSoldier { get; set; }
    // Nullable: the date pickers can be cleared by the user; Request validates.
    public DateTime? NewStart { get; set; } = DateTime.Today;
    public DateTime? NewEnd { get; set; } = DateTime.Today.AddDays(3);
    public string NewReason { get; set; } = string.Empty;

    private LeaveStatus? _statusFilter;
    public LeaveStatus? StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (_statusFilter == value) return;
            _statusFilter = value; OnPropertyChanged();
            ClearStatusFilterCommand.NotifyCanExecuteChanged();
            _ = LoadAsync(); // filter changes apply immediately
        }
    }
    public Array Statuses => Enum.GetValues(typeof(LeaveStatus));

    [RelayCommand(CanExecute = nameof(CanClearStatusFilter))]
    private void ClearStatusFilter() => StatusFilter = null;
    private bool CanClearStatusFilter() => StatusFilter is not null;

    private LeaveDto? _selected;
    public LeaveDto? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); ApproveCommand.NotifyCanExecuteChanged(); RejectCommand.NotifyCanExecuteChanged(); }
    }

    public LeavesViewModel(ISender sender, IDialogService dialogs, IPrintService print)
    { _sender = sender; _dialogs = dialogs; _print = print; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var items = await _sender.Send(new ListLeavesQuery(StatusFilter));
            Items.Clear();
            foreach (var l in items) Items.Add(l);
            PrintCommand.NotifyCanExecuteChanged();
            ExportPdfCommand.NotifyCanExecuteChanged();
        });
    }

    [RelayCommand]
    private async Task LoadSoldiersAsync()
    {
        await RunAsync(async () =>
        {
            var filter = new SoldierSearchRequest(null, null, true, null, 1, 500);
            var result = await _sender.Send(new SearchSoldiersQuery(filter));
            Soldiers.Clear();
            foreach (var s in result.Items.OrderBy(s => s.LastName)) Soldiers.Add(s);
        });
    }

    [RelayCommand]
    private async Task RequestAsync()
    {
        if (NewSoldier is not { } soldier) { ErrorMessage = "یک سرباز از فهرست انتخاب کنید."; return; }
        if (NewStart is null || NewEnd is null) { ErrorMessage = "تاریخ شروع و پایان مرخصی را وارد کنید."; return; }
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new CreateLeaveCommand(soldier.Id,
                DateOnly.FromDateTime(NewStart.Value), DateOnly.FromDateTime(NewEnd.Value), NewReason));
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            NewReason = string.Empty; NewSoldier = null;
            OnPropertyChanged(nameof(NewReason)); OnPropertyChanged(nameof(NewSoldier));
            await LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanDecide))]
    private async Task ApproveAsync()
    {
        if (Selected is null) return;
        // Mirrors the schedule-approve confirmation: an irreversible, audited
        // decision sitting one click away in a dense toolbar needs a safety net.
        if (!_dialogs.Confirm(
            $"مرخصی «{Selected.SoldierName ?? $"#{Selected.SoldierId}"}» تأیید شود؟ " +
            "این عملیات در گزارش حسابرسی ثبت می‌شود و از این صفحه قابل بازگردانی نیست.")) return;
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new ApproveLeaveCommand(Selected.Id));
            if (!r.IsSuccess) _dialogs.Error(r.Error); else await LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanDecide))]
    private async Task RejectAsync()
    {
        if (Selected is null) return;
        var reason = InputDialog.Prompt("دلیل رد مرخصی:", "رد مرخصی", "");
        if (string.IsNullOrWhiteSpace(reason)) return;
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new RejectLeaveCommand(Selected.Id, reason));
            if (!r.IsSuccess) _dialogs.Error(r.Error); else await LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void Print()
    {
        try
        {
            _print.Print(BuildReport(), "سوابق مرخصی");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Print failed in LeavesViewModel.");
            _dialogs.Error("چاپ انجام نشد. از اتصال و روشن بودن چاپگر اطمینان حاصل کنید.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void ExportPdf()
    {
        try
        {
            _print.ExportToPdf(BuildReport(), "سوابق مرخصی.pdf");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "PDF export failed in LeavesViewModel.");
            _dialogs.Error("ساخت فایل PDF انجام نشد.");
        }
    }

    private bool CanPrint() => Items.Count > 0;

    private System.Windows.Documents.FlowDocument BuildReport()
    {
        return _print.BuildTableReport(
            "سوابق مرخصی", PersianDate.ToPersianDigits($"{Items.Count} رکورد"),
            new[] { "شناسه", "سرباز", "شروع", "پایان", "وضعیت", "علت" },
            Items.Select(l => new[]
            {
                PersianDate.ToPersianDigits(l.Id.ToString()),
                l.SoldierName ?? PersianDate.ToPersianDigits(l.SoldierId.ToString()),
                PersianDate.ToJalali(l.StartDate), PersianDate.ToJalali(l.EndDate),
                EnumText.Describe(l.Status), l.Reason
            }));
    }

    // Only pending requests can be approved/rejected; the domain enforces this
    // too, but the buttons should not invite an action that will always fail.
    private bool CanDecide() => Selected is { Status: LeaveStatus.Requested };
}
