using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Users;
using MilOps.Domain.Enums;
using MilOps.Presentation.Services;
using MilOps.Presentation.Views;

namespace MilOps.Presentation.ViewModels;

/// <summary>User management (Commander-only): create, change password, deactivate.</summary>
public sealed partial class UsersViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly IDialogService _dialogs;

    public ObservableCollection<UserDto> Items { get; } = new();
    public Array Roles => Enum.GetValues(typeof(Role));

    public string NewFullName { get; set; } = string.Empty;
    public string NewUsername { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public Role NewRole { get; set; } = Role.Operator;

    private UserDto? _selected;
    public UserDto? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); ChangePasswordCommand.NotifyCanExecuteChanged(); DeactivateCommand.NotifyCanExecuteChanged(); }
    }

    public UsersViewModel(ISender sender, IDialogService dialogs) { _sender = sender; _dialogs = dialogs; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var items = await _sender.Send(new ListUsersQuery());
            Items.Clear();
            foreach (var u in items) Items.Add(u);
        });
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new CreateUserCommand(NewFullName, NewUsername, NewRole, NewPassword));
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            NewFullName = NewUsername = NewPassword = string.Empty;
            OnPropertyChanged(nameof(NewFullName)); OnPropertyChanged(nameof(NewUsername)); OnPropertyChanged(nameof(NewPassword));
            await LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task ChangePasswordAsync()
    {
        if (Selected is null) return;
        var pw = InputDialog.Prompt($"New password for '{Selected.Username}':", "Change Password", "");
        if (string.IsNullOrWhiteSpace(pw)) return;
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new ChangePasswordCommand(Selected.Id, pw));
            if (!r.IsSuccess) _dialogs.Error(r.Error);
            else _dialogs.Info("Password updated and recorded in the audit log.");
        });
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task DeactivateAsync()
    {
        if (Selected is null) return;
        if (!_dialogs.Confirm($"Deactivate user '{Selected.Username}'? They will not be able to log in.")) return;
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new DeactivateUserCommand(Selected.Id));
            if (!r.IsSuccess) _dialogs.Error(r.Error); else await LoadAsync();
        });
    }

    private bool CanAct() => Selected is not null;
}
