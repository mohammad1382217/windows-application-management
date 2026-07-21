using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Schedules;
using MilOps.Application.Soldiers;
using MilOps.Domain.Enums;
using MilOps.Presentation.Common;

namespace MilOps.Presentation.ViewModels;

public sealed class AssignmentRowVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    // Picked from the soldier list — no more free-typed IDs.
    private SoldierDto? _selectedSoldier;
    public SoldierDto? SelectedSoldier { get => _selectedSoldier; set { _selectedSoldier = value; Notify(nameof(SelectedSoldier)); } }

    private GuardPost _post = GuardPost.Guard;
    public GuardPost Post { get => _post; set { _post = value; Notify(nameof(Post)); } }

    private ShiftNumber _shift = ShiftNumber.First;
    public ShiftNumber Shift { get => _shift; set { _shift = value; Notify(nameof(Shift)); } }

    public string? ShiftStart { get; set; }
    public string? ShiftEnd { get; set; }
    public string? Note { get; set; }
}

public sealed partial class ScheduleBuilderViewModel : ViewModelBase
{
    private readonly ISender _sender;

    public event Action? Saved;
    public event Action? Cancelled;

    public ObservableCollection<AssignmentRowVm> Assignments { get; } = new();

    /// <summary>Active, guard-eligible soldiers offered by the row picker.</summary>
    public ObservableCollection<SoldierDto> Soldiers { get; } = new();

    public Array GuardPosts => Enum.GetValues(typeof(GuardPost));
    public Array Shifts => Enum.GetValues(typeof(ShiftNumber));

    private DateTime _date = DateTime.Today;
    public DateTime Date { get => _date; set { _date = value; OnPropertyChanged(); } }

    private string _remarks = string.Empty;
    public string Remarks { get => _remarks; set { _remarks = value; OnPropertyChanged(); } }

    /// <summary>When set (before Initialize), the builder edits this schedule instead of creating one.</summary>
    public int? EditScheduleId { get; set; }
    public bool IsEditMode => EditScheduleId is not null;
    public string WindowTitle => IsEditMode ? "ویرایش برنامه نگهبانی" : "افزودن برنامه نگهبانی";

    private ScheduleStatus? _status;
    /// <summary>True once an Approved/Printed schedule is loaded — the board becomes read-only.</summary>
    public bool IsLocked => _status is ScheduleStatus.Approved or ScheduleStatus.Printed;
    public bool IsNotLocked => !IsLocked;

    public ScheduleBuilderViewModel(ISender sender) => _sender = sender;

    /// <summary>Loads the soldier picker, then (in edit mode) the schedule being edited.</summary>
    [RelayCommand]
    private async Task InitializeAsync()
    {
        await LoadSoldiersAsync();
        if (EditScheduleId is { } id) await LoadForEditAsync(id);
    }

    private async Task LoadForEditAsync(int scheduleId)
    {
        await RunAsync(async () =>
        {
            var dto = await _sender.Send(new GetScheduleByIdQuery(scheduleId));
            if (dto is null) { ErrorMessage = "برنامه یافت نشد."; return; }

            Date = dto.Date.ToDateTime(TimeOnly.MinValue);
            Remarks = dto.Remarks ?? string.Empty;
            _status = dto.Status;
            OnPropertyChanged(nameof(IsLocked));
            OnPropertyChanged(nameof(IsNotLocked));
            SaveCommand.NotifyCanExecuteChanged();
            Assignments.Clear();
            foreach (var a in dto.Assignments)
            {
                Assignments.Add(new AssignmentRowVm
                {
                    // Soldiers no longer guard-eligible aren't in the picker; their
                    // rows open unselected so the user consciously re-assigns them.
                    SelectedSoldier = Soldiers.FirstOrDefault(s => s.Id == a.SoldierId),
                    Post = a.Post,
                    Shift = a.Shift,
                    ShiftStart = a.ShiftStart?.ToString("HH\\:mm"),
                    ShiftEnd = a.ShiftEnd?.ToString("HH\\:mm"),
                    Note = a.Note,
                });
            }
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
            // Only guard-eligible soldiers (active + not health-restricted) are
            // offered; assigning a restricted soldier is a decision the domain
            // doesn't forbid, but the picker shouldn't nudge toward it.
            foreach (var s in result.Items.Where(s => s.CanGuard).OrderBy(s => s.LastName))
                Soldiers.Add(s);
        });
    }

    [RelayCommand]
    private void AddRow() => Assignments.Add(new AssignmentRowVm());

    [RelayCommand]
    private void RemoveRow(AssignmentRowVm row) => Assignments.Remove(row);

    private bool CanSave() => IsNotLocked;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (Assignments.Count == 0) { ErrorMessage = "حداقل یک ردیف نگهبانی وارد کنید."; return; }

        var rows = new List<GuardAssignmentDto>();
        for (var i = 0; i < Assignments.Count; i++)
        {
            var row = Assignments[i];
            // Errors used to echo only the bad text back with no row reference —
            // with several rows the user had to visually hunt for the offender.
            var rowLabel = $"ردیف {PersianDate.ToPersianDigits((i + 1).ToString())}" +
                (row.SelectedSoldier is { } s ? $" ({s.DisplayName})" : string.Empty);

            if (row.SelectedSoldier is not { } soldier)
            {
                ErrorMessage = $"{rowLabel}: یک سرباز از فهرست انتخاب کنید.";
                return;
            }
            var sid = soldier.Id;
            TimeOnly? start = null, end = null;
            if (!string.IsNullOrWhiteSpace(row.ShiftStart))
            {
                if (!TryParseTime(row.ShiftStart, out var t))
                { ErrorMessage = $"{rowLabel}: فرمت ساعت شروع اشتباه است ({row.ShiftStart}) — مثال: ۰۸:۰۰"; return; }
                start = t;
            }
            if (!string.IsNullOrWhiteSpace(row.ShiftEnd))
            {
                if (!TryParseTime(row.ShiftEnd, out var t))
                { ErrorMessage = $"{rowLabel}: فرمت ساعت پایان اشتباه است ({row.ShiftEnd}) — مثال: ۱۶:۰۰"; return; }
                end = t;
            }
            rows.Add(new GuardAssignmentDto(sid, row.Post, row.Shift, start, end,
                string.IsNullOrWhiteSpace(row.Note) ? null : row.Note));
        }

        await RunAsync(async () =>
        {
            var date = DateOnly.FromDateTime(Date);
            var remarks = string.IsNullOrWhiteSpace(Remarks) ? null : Remarks;
            if (EditScheduleId is { } id)
            {
                var r = await _sender.Send(new UpdateScheduleCommand(id, date, remarks, rows));
                if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            }
            else
            {
                var r = await _sender.Send(new CreateScheduleCommand(date, remarks, rows));
                if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            }
            Saved?.Invoke();
        });

        // This window has no per-field error placements; fold any pipeline
        // validation failures into the single visible error line.
        if (ErrorMessage is null && FieldErrors.Count > 0)
            ErrorMessage = string.Join("\n", FieldErrors.Values.Distinct());
    }

    private static bool TryParseTime(string text, out TimeOnly time) =>
        TimeOnly.TryParse(PersianDate.ToLatinDigits(text.Trim()),
            CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();
}
