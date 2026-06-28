using System.IO;
using System.Windows;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MilOps.Application;
using MilOps.Infrastructure;
using MilOps.Infrastructure.Logging;
using MilOps.Presentation.Services;
using MilOps.Presentation.ViewModels;
using MilOps.Presentation.Views;
using Serilog;

namespace MilOps.Presentation;

public partial class App : System.Windows.Application
{
    private readonly IHost _host;

    public App()
    {
        // Build the fully-configured logger (rolling file sink etc.) ONCE and make
        // it the static Log.Logger. The no-arg UseSerilog() below routes all
        // Microsoft.Extensions.Logging through it.
        //
        // NOTE: the previous `UseSerilog((_, cfg) => CreateLogger())` bound to the
        // Action overload, which DISCARDED the returned logger and installed the
        // empty (sink-less) `cfg` it passed in — so nothing was ever written.
        Log.Logger = LoggingConfiguration.CreateLogger();

        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureAppConfiguration((_, cfg) =>
            {
                cfg.SetBasePath(AppContext.BaseDirectory);
                cfg.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddApplication(ctx.Configuration);
                services.AddInfrastructure(ctx.Configuration);

                // Presentation services
                services.AddSingleton<INavigationService, NavigationService>();
                services.AddSingleton<IDialogService, DialogService>();
                services.AddSingleton<IPrintService, PrintService>();

                // Windows
                services.AddTransient<LoginWindow>();
                services.AddTransient<MainWindow>();

                // ViewModels
                services.AddTransient<LoginViewModel>();
                services.AddTransient<MainViewModel>();
                services.AddTransient<SoldiersViewModel>();
                services.AddTransient<SoldierEditorViewModel>();
                services.AddTransient<TokensViewModel>();
                services.AddTransient<SchedulesViewModel>();
                services.AddTransient<ScheduleBuilderViewModel>();
                services.AddTransient<WeaponsViewModel>();
                services.AddTransient<LeavesViewModel>();
                services.AddTransient<UsersViewModel>();
                services.AddTransient<AuditViewModel>();
            })
            .Build();
    }

    public static IServiceProvider Services =>
        ((App)Current)._host.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Fatal-error guard: never crash silently; show the user a message.
        // The log sink is async (buffered), so we MUST flush before the process
        // dies or the crash details never reach disk.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Log.Fatal((Exception)args.ExceptionObject, "Unhandled AppDomain exception.");
            Log.CloseAndFlush();
        };
        DispatcherUnhandledException += (_, args) =>
        {
            Log.Error(args.Exception, "Dispatcher unhandled exception.");
            MessageBox.Show(
                "An unexpected error occurred.\n\n" + FlattenException(args.Exception),
                "MilOps — Error", MessageBoxButton.OK, MessageBoxImage.Error);
            // Keep the app alive so a single UI fault doesn't kill the session;
            // the error is logged and shown.
            args.Handled = true;
        };

        await _host.StartAsync();

        // Initialize + seed the encrypted database (creates default commander on first run).
        try
        {
            await _host.Services.InitializeDatabaseAsync();
            Log.Information("Database initialized.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Database initialization failed.");
            Log.CloseAndFlush();
            MessageBox.Show(
                "Failed to initialize the encrypted database. See the log file for details.\n\n" +
                FlattenException(ex),
                "MilOps — Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        // StartupUri is intentionally not set in App.xaml; we resolve the login
        // window through DI so its LoginViewModel dependency is injected.
        try
        {
            var login = _host.Services.GetRequiredService<LoginWindow>();
            login.Show();
            MainWindow = login; // treat login as the main window until auth succeeds
            Log.Information("Login window shown.");
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Failed to create or show the login window.");
            Log.CloseAndFlush();
            MessageBox.Show(
                "Failed to open the login window. See the log file for details.\n\n" +
                FlattenException(ex),
                "MilOps — Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        using (_host) { await _host.StopAsync(TimeSpan.FromSeconds(5)); }
        Log.Information("Application exited.");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary>
    /// Walks the full exception chain (including aggregate and reflection
    /// exceptions) so the root cause is shown instead of a generic
    /// "See the inner exception" wrapper.
    /// </summary>
    private static string FlattenException(Exception? ex)
    {
        if (ex is null) return string.Empty;
        var sb = new System.Text.StringBuilder();
        var depth = 0;
        for (var current = ex; current is not null && depth < 8; current = current.InnerException, depth++)
        {
            if (sb.Length > 0) sb.Append(" -> ");
            var msg = string.IsNullOrWhiteSpace(current.Message) ? current.GetType().Name : current.Message;
            sb.Append($"[{current.GetType().Name}] {msg}");
        }
        return sb.ToString();
    }
}
