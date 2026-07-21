using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Soldiers;

namespace MilOps.Presentation.ViewModels;

/// <summary>Backs the small "تغییر بخش" modal — changes one soldier's department.</summary>
public sealed partial class ChangeDepartmentViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private int _soldierId;

    public event Action? Saved;
    public event Action? Cancelled;

    public string SoldierDisplayName { get; private set; } = string.Empty;
    public string CurrentDepartment { get; private set; } = string.Empty;
    public string NewDepartment { get; set; } = string.Empty;

    public ChangeDepartmentViewModel(ISender sender) => _sender = sender;

    public void Initialize(int soldierId, string soldierDisplayName, string currentDepartment)
    {
        _soldierId = soldierId;
        SoldierDisplayName = soldierDisplayName;
        CurrentDepartment = currentDepartment;
        NewDepartment = currentDepartment;
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new ChangeSoldierDepartmentCommand(_soldierId, NewDepartment));
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            Saved?.Invoke();
        });
    }

    [RelayCommand] private void Cancel() => Cancelled?.Invoke();
}
