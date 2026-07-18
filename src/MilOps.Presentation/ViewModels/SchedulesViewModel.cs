using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Schedules;
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

    public ObservableCollection<GuardScheduleSummaryDto> Schedules { get; } = new();

    private GuardScheduleSummaryDto? _selectedSchedule;
    public GuardScheduleSummaryDto? SelectedSchedule
    {
        get => _selectedSchedule;
        set { _selectedSchedule = value; OnPropertyChanged(); }
    }

    public SchedulesViewModel(ISender sender, IDialogService dialogs)
    { _sender = sender; _dialogs = dialogs; }

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
}
