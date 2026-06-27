using System.Windows;

namespace MilOps.Presentation.Services;

/// <summary>
/// Minimal navigation host for the single-window shell. Opens the main window
/// after successful login and closes the login window. View switching inside
/// the shell is handled by the MainViewModel via a bound content control.
/// </summary>
public interface INavigationService
{
    void ShowMain();
    void ShowLogin();
}

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _services;
    public NavigationService(IServiceProvider services) => _services = services;

    public void ShowMain()
    {
        var login = System.Windows.Application.Current.Windows.OfType<object>()
            .FirstOrDefault(w => w is LoginWindow);
        var main = _services.GetRequiredService<MainWindow>();
        System.Windows.Application.Current.MainWindow = main;
        main.Show();
        if (login is Window lw) lw.Close();
    }

    public void ShowLogin()
    {
        var login = _services.GetRequiredService<LoginWindow>();
        System.Windows.Application.Current.MainWindow = login;
        login.Show();
    }
}
