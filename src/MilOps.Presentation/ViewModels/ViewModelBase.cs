using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using MilOps.Application.Security;
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

    /// <summary>
    /// Per-field validation errors keyed by the COMMAND property name (e.g.
    /// "Username"), for inline display under each input:
    ///   Text="{Binding FieldErrors[Username]}"
    /// A missing key simply binds to nothing, so views stay declarative.
    /// </summary>
    public Dictionary<string, string> FieldErrors { get; private set; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Run an async handler with busy/error handling on the UI thread.</summary>
    protected async Task RunAsync(Func<Task> work, string busyMessage = "Working...")
    {
        ErrorMessage = null;
        ClearFieldErrors();
        IsBusy = true;
        try { await work(); }
        catch (FluentValidation.ValidationException vex)
        {
            // Route each failure to its own field; first message per field wins.
            var map = new Dictionary<string, string>();
            foreach (var f in vex.Errors)
                if (!string.IsNullOrEmpty(f.PropertyName) && !map.ContainsKey(f.PropertyName))
                    map[f.PropertyName] = f.ErrorMessage;
            FieldErrors = map;
            OnPropertyChanged(nameof(FieldErrors));

            // Errors without a property (or none mapped) still need a place to show.
            if (map.Count == 0)
                ErrorMessage = vex.Errors.FirstOrDefault()?.ErrorMessage
                               ?? "اطلاعات واردشده معتبر نیست.";
        }
        catch (AuthorizationException)
        {
            ErrorMessage = "شما مجوز انجام این عملیات را ندارید.";
        }
        catch (Exception ex)
        {
            // Never leak a raw exception message (often English/technical —
            // SQL errors, network faults) to a non-technical Persian-speaking
            // user. Log the full exception so failures stay traceable.
            Log.Error(ex, "Unhandled error in view-model operation.");
            ErrorMessage = "خطا در برقراری ارتباط با سامانه. لطفاً دوباره تلاش کنید.";
        }
        finally { IsBusy = false; }
    }

    private void ClearFieldErrors()
    {
        if (FieldErrors.Count == 0) return;
        FieldErrors = new Dictionary<string, string>();
        OnPropertyChanged(nameof(FieldErrors));
    }
}
