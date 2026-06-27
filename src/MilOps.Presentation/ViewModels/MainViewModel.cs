using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Security;
using MilOps.Presentation.Services;

namespace MilOps.Presentation.ViewModels;

/// <summary>
/// Shell view model. Holds the currently-selected feature view model, the
/// signed-in user banner, and the Logout command. Feature VMs are resolved
/// on demand from DI (scoped), so each carries its own DbContext scope.
/// </summary>
public sealed partial class MainViewModel : ViewModelBase
{
    private readonly IServiceProvider _services;
    private readonly ICurrentUser _user;
    private readonly INavigationService _nav;
    private readonly IDialogService _dialogs;
    private readonly ISender _sender;

    private ViewModelBase? _current;
    private string _currentTitle = "میز کار";
    private IServiceScope? _currentScope;

    public MainViewModel(IServiceProvider services, ICurrentUser user,
        INavigationService nav, IDialogService dialogs, ISender sender)
    {
        _services = services; _user = user; _nav = nav; _dialogs = dialogs; _sender = sender;
        UserBanner = $"{_user.FullName} ({_user.Role})";
    }

    public string UserBanner { get; }

    public ViewModelBase? Current
    {
        get => _current;
        private set { _current = value; OnPropertyChanged(); }
    }

    public string CurrentTitle
    {
        get => _currentTitle;
        private set { _currentTitle = value; OnPropertyChanged(); }
    }

    // Role-gated navigation commands (each checks a permission).
    public bool CanManageUsers => _user.Has(Permission.UserManage);
    public bool CanManageTokens => _user.Has(Permission.TokenManage);
    public bool CanManageSoldiers => _user.Has(Permission.SoldierRead);
    public bool CanManageSchedules => _user.Has(Permission.ScheduleRead);
    public bool CanManageWeapons => _user.Has(Permission.WeaponRead);
    public bool CanManageLeaves => _user.Has(Permission.LeaveRead);
    public bool CanViewAudit => _user.Has(Permission.AuditRead);

    [RelayCommand] private void ShowSoldiers() => Navigate<SoldiersViewModel>("سربازان");
    [RelayCommand] private void ShowSchedules() => Navigate<SchedulesViewModel>("برنامه نگهبانی روزانه");
    [RelayCommand] private void ShowWeapons() => Navigate<WeaponsViewModel>("تسلیحات و مهمات");
    [RelayCommand] private void ShowLeaves() => Navigate<LeavesViewModel>("مدیریت مرخصی");
    [RelayCommand] private void ShowTokens() => Navigate<TokensViewModel>("توکن‌های فرمانده");
    [RelayCommand] private void ShowUsers() => Navigate<UsersViewModel>("مدیریت کاربران");
    [RelayCommand] private void ShowAudit() => Navigate<AuditViewModel>("گزارش حسابرسی");

    private void Navigate<T>(string title) where T : ViewModelBase
    {
        // Dispose the previous feature scope (and its DbContext) before opening
        // a new one, so navigating between modules does not leak contexts.
        _currentScope?.Dispose();
        _currentScope = _services.CreateScope();
        var vm = _currentScope.ServiceProvider.GetRequiredService<T>();
        Current = vm;
        CurrentTitle = title;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (!_dialogs.Confirm("از سامانه میل‌اپس خارج می‌شوید؟")) return;
        await _sender.Send(new MilOps.Application.Authentication.LogoutCommand());
        _nav.ShowLogin();
        foreach (Window w in System.Windows.Application.Current.Windows)
            if (w is MainWindow) w.Close();
    }
}
