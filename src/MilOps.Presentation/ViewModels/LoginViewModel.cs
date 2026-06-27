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

    private string _username = string.Empty;
    private string _password = string.Empty;
    private string? _hint;

    public LoginViewModel(ISender sender, INavigationService nav, IDialogService dialogs)
    { _sender = sender; _nav = nav; _dialogs = dialogs; }

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

    public bool CanLogin => !IsBusy && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        await RunAsync(async () =>
        {
            var result = await _sender.Send(new LoginCommand(Username.Trim(), Password));
            if (result is { IsSuccess: true, Value: not null })
            {
                _nav.ShowMain();
            }
            else
            {
                Hint = result.Error;
                // Clear the password from memory-binding on failure.
                Password = string.Empty;
            }
        });
    }

    // Keep CanLogin in sync as busy changes.
    protected override void OnPropertyChanged(string? name = null)
    {
        base.OnPropertyChanged(name);
        if (name is nameof(IsBusy) or nameof(Username) or nameof(Password))
            LoginCommand.NotifyCanExecuteChanged();
    }
}
