using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Schedules;
using MilOps.Application.Security;
using MilOps.Domain.Enums;
using MilOps.Presentation.Services;
using MilOps.Presentation.Views;

namespace MilOps.Presentation.ViewModels;

/// <summary>
/// Daily guard schedules (Lohe Posti) as a flat list. Each row opens a
/// preview modal (board + print/PDF) or the builder modal in edit mode;
/// creating a new schedule opens the same builder empty.
/// </summary>
public sealed partial class SchedulesViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly ICurrentUser _user;

    public ObservableCollection<GuardScheduleSummaryDto> Schedules { get; } = new();

    /// <summary>Whether this user may reopen a finalized schedule (Commander-only).</summary>
    public bool CanManageReopen => _user.Has(Permission.ScheduleReopen);

    private GuardScheduleSummaryDto? _selectedSchedule;
    public GuardScheduleSummaryDto? SelectedSchedule
    {
        get => _selectedSchedule;
        set { _selectedSchedule = value; OnPropertyChanged();
            FinalizeCommand.NotifyCanExecuteChanged(); ReopenCommand.NotifyCanExecuteChanged(); }
    }

    public SchedulesViewModel(ISender sender, IDialogService dialogs, ICurrentUser user)
    { _sender = sender; _dialogs = dialogs; _user = user; }

    [RelayCommand]
    private async Task LoadSchedulesAsync()
    {
        await RunAsync(async () =>
        {
            var items = await _sender.Send(new ListSchedulesQuery());
            Schedules.Clear();
            foreach (var s in items) Schedules.Add(s);
        });
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        var builder = new ScheduleBuilderWindow(DateTime.Today)
        { Owner = System.Windows.Application.Current.MainWindow };
        if (builder.ShowDialog() != true) return;
        await LoadSchedulesAsync();
    }

    /// <summary>Opens the builder pre-filled with the row's schedule (per-row "ویرایش").</summary>
    [RelayCommand]
    private async Task EditAsync(GuardScheduleSummaryDto? row)
    {
        row ??= SelectedSchedule;
        if (row is null) { _dialogs.Warning("ابتدا یک برنامه را از فهرست انتخاب کنید."); return; }

        var builder = new ScheduleBuilderWindow(editScheduleId: row.Id)
        { Owner = System.Windows.Application.Current.MainWindow };
        if (builder.ShowDialog() != true) return;
        await LoadSchedulesAsync();
    }

    /// <summary>Opens the read-only board modal with print/PDF (per-row "پیش‌نمایش").</summary>
    [RelayCommand]
    private async Task PreviewAsync(GuardScheduleSummaryDto? row)
    {
        row ??= SelectedSchedule;
        if (row is null) { _dialogs.Warning("ابتدا یک برنامه را از فهرست انتخاب کنید."); return; }

        GuardScheduleDto? dto = null;
        await RunAsync(async () => dto = await _sender.Send(new GetScheduleByIdQuery(row.Id)));
        if (dto is null) { _dialogs.Error("برنامه یافت نشد — فهرست را بازخوانی کنید."); return; }

        new SchedulePreviewWindow(dto)
        { Owner = System.Windows.Application.Current.MainWindow }.ShowDialog();
    }

    private static bool CanFinalize(GuardScheduleSummaryDto? row) =>
        row?.Status == ScheduleStatus.Draft;

    [RelayCommand(CanExecute = nameof(CanFinalize))]
    private async Task FinalizeAsync(GuardScheduleSummaryDto? row)
    {
        row ??= SelectedSchedule;
        if (row is null) { _dialogs.Warning("ابتدا یک برنامه را از فهرست انتخاب کنید."); return; }
        if (!_dialogs.Confirm("این برنامه ثبت نهایی و قفل می‌شود. ادامه می‌دهید؟")) return;

        await RunAsync(async () =>
        {
            var r = await _sender.Send(new ApproveScheduleCommand(row.Id));
            if (!r.IsSuccess) { _dialogs.Error(r.Error); return; }
            await LoadSchedulesAsync();
        });
    }

    private bool CanReopen(GuardScheduleSummaryDto? row) =>
        CanManageReopen && row?.Status == ScheduleStatus.Approved;

    [RelayCommand(CanExecute = nameof(CanReopen))]
    private async Task ReopenAsync(GuardScheduleSummaryDto? row)
    {
        row ??= SelectedSchedule;
        if (row is null) { _dialogs.Warning("ابتدا یک برنامه را از فهرست انتخاب کنید."); return; }
        if (!_dialogs.Confirm("برنامه به حالت پیش‌نویس بازمی‌گردد و قابل ویرایش می‌شود. ادامه می‌دهید؟")) return;

        await RunAsync(async () =>
        {
            var r = await _sender.Send(new ReopenScheduleCommand(row.Id));
            if (!r.IsSuccess) { _dialogs.Error(r.Error); return; }
            await LoadSchedulesAsync();
        });
    }
}
