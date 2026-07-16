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

    public ScheduleBuilderViewModel(ISender sender) => _sender = sender;

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

    [RelayCommand]
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
            var r = await _sender.Send(new CreateScheduleCommand(
                DateOnly.FromDateTime(Date),
                string.IsNullOrWhiteSpace(Remarks) ? null : Remarks,
                rows));
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
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
