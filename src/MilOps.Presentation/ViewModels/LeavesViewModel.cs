using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Leaves;
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

    public int NewSoldierId { get; set; }
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
        });
    }

    [RelayCommand]
    private async Task RequestAsync()
    {
        if (NewSoldierId <= 0) { ErrorMessage = "شناسه سرباز الزامی است."; return; }
        if (NewStart is null || NewEnd is null) { ErrorMessage = "تاریخ شروع و پایان مرخصی را وارد کنید."; return; }
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new CreateLeaveCommand(NewSoldierId,
                DateOnly.FromDateTime(NewStart.Value), DateOnly.FromDateTime(NewEnd.Value), NewReason));
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            NewReason = string.Empty; NewSoldierId = 0;
            OnPropertyChanged(nameof(NewReason)); OnPropertyChanged(nameof(NewSoldierId));
            await LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanDecide))]
    private async Task ApproveAsync()
    {
        if (Selected is null) return;
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
            PrintCore();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Print failed in LeavesViewModel.");
            _dialogs.Error("چاپ انجام نشد. از اتصال و روشن بودن چاپگر اطمینان حاصل کنید.");
        }
    }

    private bool CanPrint() => Items.Count > 0;

    private void PrintCore()
    {
        var doc = _print.BuildTableReport(
            "سوابق مرخصی", PersianDate.ToPersianDigits($"{Items.Count} رکورد"),
            new[] { "شناسه", "سرباز", "شروع", "پایان", "وضعیت", "علت" },
            Items.Select(l => new[]
            {
                PersianDate.ToPersianDigits(l.Id.ToString()),
                PersianDate.ToPersianDigits(l.SoldierId.ToString()),
                PersianDate.ToJalali(l.StartDate), PersianDate.ToJalali(l.EndDate),
                EnumText.Describe(l.Status), l.Reason
            }));
        _print.Print(doc, "سوابق مرخصی");
    }

    // Only pending requests can be approved/rejected; the domain enforces this
    // too, but the buttons should not invite an action that will always fail.
    private bool CanDecide() => Selected is { Status: LeaveStatus.Requested };
}
