using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace MilOps.Presentation.ViewModels;

/// <summary>
/// Base for all view models. Implements INPC, exposes a busy flag, and a
/// localized error message for binding. Uses CommunityToolkit.Mvvm for
/// [RelayCommand] generation. ViewModels depend ONLY on Application-layer
/// abstractions (MediatR via ISender, ICurrentUser, services).
/// </summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    private bool _isBusy;
    private string? _errorMessage;

    public bool IsBusy
    {
        get => _isBusy;
        protected set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotBusy)); }
    }

    public bool IsNotBusy => !IsBusy;

    public string? ErrorMessage
    {
        get => _errorMessage;
        protected set { _errorMessage = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Run an async handler with busy/error handling on the UI thread.</summary>
    protected async Task RunAsync(Func<Task> work, string busyMessage = "Working...")
    {
        ErrorMessage = null;
        IsBusy = true;
        try { await work(); }
        catch (Exception ex)
        {
            // Surface a friendly message to the UI, but still log the full
            // exception so failures are traceable rather than silently lost.
            Log.Error(ex, "Unhandled error in view-model operation.");
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }
}
