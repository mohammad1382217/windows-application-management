using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Schedules;
using MilOps.Domain.Enums;
using MilOps.Presentation.Common;

namespace MilOps.Presentation.ViewModels;

public sealed class AssignmentRowVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    private string _soldierId = string.Empty;
    public string SoldierId { get => _soldierId; set { _soldierId = value; Notify(nameof(SoldierId)); } }

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

    public Array GuardPosts => Enum.GetValues(typeof(GuardPost));
    public Array Shifts => Enum.GetValues(typeof(ShiftNumber));

    private DateTime _date = DateTime.Today;
    public DateTime Date { get => _date; set { _date = value; OnPropertyChanged(); } }

    private string _remarks = string.Empty;
    public string Remarks { get => _remarks; set { _remarks = value; OnPropertyChanged(); } }

    public ScheduleBuilderViewModel(ISender sender) => _sender = sender;

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
        foreach (var row in Assignments)
        {
            // The UI hints show Persian digits (۰۸:۰۰), so accept them here too.
            if (!int.TryParse(PersianDate.ToLatinDigits(row.SoldierId), out var sid) || sid <= 0)
            {
                ErrorMessage = "شناسه سرباز باید عدد صحیح مثبت باشد.";
                return;
            }
            TimeOnly? start = null, end = null;
            if (!string.IsNullOrWhiteSpace(row.ShiftStart))
            {
                if (!TryParseTime(row.ShiftStart, out var t)) { ErrorMessage = $"فرمت ساعت شروع اشتباه است: {row.ShiftStart}  (مثال: ۰۸:۰۰)"; return; }
                start = t;
            }
            if (!string.IsNullOrWhiteSpace(row.ShiftEnd))
            {
                if (!TryParseTime(row.ShiftEnd, out var t)) { ErrorMessage = $"فرمت ساعت پایان اشتباه است: {row.ShiftEnd}  (مثال: ۱۶:۰۰)"; return; }
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
