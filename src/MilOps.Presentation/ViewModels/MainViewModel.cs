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
    private readonly ISessionTokenStore _tokenStore;

    private ViewModelBase? _current;
    private string _currentTitle = string.Empty;
    private IServiceScope? _currentScope;
    private string _activeNav = string.Empty;

    private static readonly Dictionary<Type, string> NavKeys = new()
    {
        [typeof(SoldiersViewModel)]    = "soldiers",
        [typeof(AttendanceViewModel)]  = "attendance",
        [typeof(SchedulesViewModel)]   = "schedules",
        [typeof(WeaponsViewModel)]     = "weapons",
        [typeof(LeavesViewModel)]      = "leaves",
        [typeof(TokensViewModel)]      = "tokens",
        [typeof(UsersViewModel)]       = "users",
        [typeof(AuditViewModel)]       = "audit",
        [typeof(SettingsViewModel)]    = "settings",
    };

    public MainViewModel(IServiceProvider services, ICurrentUser user,
        INavigationService nav, IDialogService dialogs, ISender sender,
        ISessionTokenStore tokenStore)
    {
        _services = services; _user = user; _nav = nav; _dialogs = dialogs; _sender = sender;
        _tokenStore = tokenStore;
        UserBanner = $"{_user.FullName} — {_user.Role}";
        NavigateHome();
    }

    /// <summary>First view the user actually has permission to see.</summary>
    private void NavigateHome()
    {
        if (CanManageSoldiers) Navigate<SoldiersViewModel>("سربازان");
        else if (CanManageSchedules) Navigate<SchedulesViewModel>("برنامه نگهبانی روزانه");
        else if (CanManageWeapons) Navigate<WeaponsViewModel>("تسلیحات و مهمات");
        else if (CanManageLeaves) Navigate<LeavesViewModel>("مدیریت مرخصی");
        else if (CanManageTokens) Navigate<TokensViewModel>("توکن‌های فرمانده");
        else if (CanManageUsers) Navigate<UsersViewModel>("مدیریت کاربران");
        else if (CanViewAudit) Navigate<AuditViewModel>("گزارش حسابرسی");
    }

    public string UserBanner { get; }

    public string ActiveNav
    {
        get => _activeNav;
        private set
        {
            if (_activeNav == value) return;
            _activeNav = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsSoldiersActive));
            OnPropertyChanged(nameof(IsAttendanceActive));
            OnPropertyChanged(nameof(IsSchedulesActive));
            OnPropertyChanged(nameof(IsWeaponsActive));
            OnPropertyChanged(nameof(IsLeavesActive));
            OnPropertyChanged(nameof(IsTokensActive));
            OnPropertyChanged(nameof(IsUsersActive));
            OnPropertyChanged(nameof(IsAuditActive));
            OnPropertyChanged(nameof(IsSettingsActive));
        }
    }

    public bool IsSoldiersActive   => ActiveNav == "soldiers";
    public bool IsAttendanceActive => ActiveNav == "attendance";
    public bool IsSchedulesActive  => ActiveNav == "schedules";
    public bool IsWeaponsActive    => ActiveNav == "weapons";
    public bool IsLeavesActive     => ActiveNav == "leaves";
    public bool IsTokensActive     => ActiveNav == "tokens";
    public bool IsUsersActive      => ActiveNav == "users";
    public bool IsAuditActive      => ActiveNav == "audit";
    public bool IsSettingsActive   => ActiveNav == "settings";

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
    public bool CanManageAttendance => _user.Has(Permission.AttendanceRead);
    public bool CanManageSchedules => _user.Has(Permission.ScheduleRead);
    public bool CanManageWeapons => _user.Has(Permission.WeaponRead);
    public bool CanManageLeaves => _user.Has(Permission.LeaveRead);
    public bool CanViewAudit => _user.Has(Permission.AuditRead);

    [RelayCommand] private void ShowSoldiers() => Navigate<SoldiersViewModel>("سربازان");
    [RelayCommand] private void ShowAttendance() => Navigate<AttendanceViewModel>("آمار و حضور و غیاب");
    [RelayCommand] private void ShowSchedules() => Navigate<SchedulesViewModel>("برنامه نگهبانی روزانه");
    [RelayCommand] private void ShowWeapons() => Navigate<WeaponsViewModel>("تسلیحات و مهمات");
    [RelayCommand] private void ShowLeaves() => Navigate<LeavesViewModel>("مدیریت مرخصی");
    [RelayCommand] private void ShowTokens() => Navigate<TokensViewModel>("توکن‌های فرمانده");
    [RelayCommand] private void ShowUsers() => Navigate<UsersViewModel>("مدیریت کاربران");
    [RelayCommand] private void ShowAudit() => Navigate<AuditViewModel>("گزارش حسابرسی");
    [RelayCommand] private void ShowSettings() => Navigate<SettingsViewModel>("تنظیمات");

    private void Navigate<T>(string title) where T : ViewModelBase
    {
        _currentScope?.Dispose();
        _currentScope = _services.CreateScope();
        var vm = _currentScope.ServiceProvider.GetRequiredService<T>();
        Current = vm;
        CurrentTitle = title;
        ActiveNav = NavKeys.TryGetValue(typeof(T), out var key) ? key : string.Empty;
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        if (!_dialogs.Confirm("از سامانه سنگر خارج می‌شوید؟")) return;
        // LogoutCommand revokes all persistent sessions server-side; the local
        // DPAPI token file must go too so the next start shows the login window.
        // Local sign-out proceeds even if the server-side revoke fails, so the
        // user is never trapped in a session they asked to leave.
        try { await _sender.Send(new MilOps.Application.Authentication.LogoutCommand()); }
        catch (Exception ex) { Serilog.Log.Error(ex, "Server-side logout failed; continuing local sign-out."); }
        _tokenStore.Delete();
        _nav.ShowLogin();
        foreach (Window w in System.Windows.Application.Current.Windows)
            if (w is MainWindow) w.Close();
    }
}
