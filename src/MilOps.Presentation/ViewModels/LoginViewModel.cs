using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Authentication;
using MilOps.Presentation.Services;

namespace MilOps.Presentation.ViewModels;

/// <summary>
/// Login screen view model. Sends a LoginCommand through MediatR and, on
/// success, hands off to the main shell via the navigation service.
/// </summary>
public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ISessionTokenStore _tokenStore;

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string? _hint;
    private string _activationToken = string.Empty;
    private bool _isActivationRequired;

    public LoginViewModel(ISender sender, INavigationService nav, IDialogService dialogs,
        ISessionTokenStore tokenStore)
    { _sender = sender; _nav = nav; _dialogs = dialogs; _tokenStore = tokenStore; }

    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); LoginCommand.NotifyCanExecuteChanged(); }
    }

    // Bound two-way from a PasswordBox via a code-behind attached property (see LoginWindow).
    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); LoginCommand.NotifyCanExecuteChanged(); }
    }

    public string? Hint
    {
        get => _hint;
        set { _hint = value; OnPropertyChanged(); }
    }

    /// <summary>Shown after the server answers ACTIVATION_REQUIRED for this account.</summary>
    public bool IsActivationRequired
    {
        get => _isActivationRequired;
        set { _isActivationRequired = value; OnPropertyChanged(); }
    }

    public string ActivationToken
    {
        get => _activationToken;
        set { _activationToken = value; OnPropertyChanged(); ActivateCommand.NotifyCanExecuteChanged(); }
    }

    public bool CanLogin => !IsBusy && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

    public bool CanActivate => CanLogin && !string.IsNullOrWhiteSpace(ActivationToken);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        Hint = null; // stale error from the previous attempt must not linger
        await RunAsync(async () =>
        {
            var result = await _sender.Send(new LoginCommand(Username.Trim(), Password));
            if (result is { IsSuccess: true, Value: not null })
            {
                // Persist the "remember me" token (DPAPI-protected) so the next
                // app start signs in silently and just rotates the token.
                if (result.Value.PersistentToken is { } token)
                    _tokenStore.Save(token);
                _nav.ShowMain();
            }
            else if (result.Code == "ACTIVATION_REQUIRED")
            {
                // First login of a commander-created account: keep the entered
                // credentials and reveal the activation-token field instead.
                Hint = result.Error;
                IsActivationRequired = true;
            }
            else
            {
                Hint = result.Error;
                // Clear the password from memory-binding on failure.
                // LoginWindow watches this and clears the PasswordBox too,
                // keeping the visual state and CanLogin in sync.
                Password = string.Empty;
            }
        });
        // Non-validation failures (DB down, crypto, ...) land in ErrorMessage;
        // surface them in the same hint area the user is already looking at.
        if (ErrorMessage is { Length: > 0 } err) Hint = err;
    }

    /// <summary>Redeems the commander-issued token, activates the account, and signs in.</summary>
    [RelayCommand(CanExecute = nameof(CanActivate))]
    private async Task ActivateAsync()
    {
        Hint = null;
        await RunAsync(async () =>
        {
            var result = await _sender.Send(new ActivateAccountCommand(
                Username.Trim(), Password, ActivationToken.Trim()));
            if (result is { IsSuccess: true, Value: not null })
            {
                if (result.Value.PersistentToken is { } token)
                    _tokenStore.Save(token);
                _nav.ShowMain();
            }
            else
            {
                Hint = result.Error;
            }
        });
        if (ErrorMessage is { Length: > 0 } err) Hint = err;
    }

    // Keep CanLogin/CanActivate in sync as busy changes.
    protected override void OnPropertyChanged(string? name = null)
    {
        base.OnPropertyChanged(name);
        if (name is nameof(IsBusy) or nameof(Username) or nameof(Password))
        {
            LoginCommand.NotifyCanExecuteChanged();
            ActivateCommand.NotifyCanExecuteChanged();
        }
    }
}
