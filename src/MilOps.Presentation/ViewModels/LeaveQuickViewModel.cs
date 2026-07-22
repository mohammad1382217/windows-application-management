using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Leaves;

namespace MilOps.Presentation.ViewModels;

/// <summary>
/// Backs the small "ثبت مرخصی" modal opened from the Attendance roll-call
/// screen — records a leave for a soldier and approves it immediately (the
/// operator recording it during roll-call is already the approving party).
/// </summary>
public sealed partial class LeaveQuickViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private int _soldierId;

    public event Action? Saved;
    public event Action? Cancelled;

    public string SoldierDisplayName { get; private set; } = string.Empty;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today;
    public string Reason { get; set; } = string.Empty;

    public LeaveQuickViewModel(ISender sender) => _sender = sender;

    public void Initialize(int soldierId, string soldierDisplayName, DateTime defaultDate)
    {
        _soldierId = soldierId;
        SoldierDisplayName = soldierDisplayName;
        StartDate = defaultDate;
        EndDate = defaultDate;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            var start = DateOnly.FromDateTime(StartDate);
            var end = DateOnly.FromDateTime(EndDate);
            var r = await _sender.Send(new CreateLeaveCommand(_soldierId, start, end, Reason));
            if (!r.IsSuccess || r.Value == 0) { ErrorMessage = r.Error; return; }

            // Recorded by the operator during roll-call — already granted, no
            // separate approval step needed from the Leaves screen.
            var approve = await _sender.Send(new ApproveLeaveCommand(r.Value));
            if (!approve.IsSuccess) { ErrorMessage = approve.Error; return; }

            Saved?.Invoke();
        });
    }

    [RelayCommand] private void Cancel() => Cancelled?.Invoke();
}
