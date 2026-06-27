using Serilog;
using Serilog.Events;
using Serilog.Filters;

namespace MilOps.Infrastructure.Logging;

/// <summary>
/// Serilog bootstrap and configuration. Logs are written to rolling files under
/// %LOCALAPPDATA%/MilOps/logs. We EXCLUDE password/token/secret-related log
/// events via a filter so secrets never reach the log sink.
/// </summary>
public static class LoggingConfiguration
{
    public static ILogger CreateLogger(string? logDirectory = null)
    {
        var dir = logDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MilOps", "logs");
        Directory.CreateDirectory(dir);

        var config = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .MinimumLevel.Override("Microsoft.EntityFrameworkCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithEnvironmentUserName()
            .Filter.ByExcluding(Matching.WithProperty<string>("Password", _ => true))
            .Filter.ByExcluding(Matching.WithProperty<string>("PasswordHash", _ => true))
            .Filter.ByExcluding(Matching.WithProperty<string>("Token", _ => true))
            .Filter.ByExcluding(Matching.WithProperty<string>("PlaintextToken", _ => true))
            .WriteTo.Async(a => a.File(
                path: Path.Combine(dir, "milops-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                shared: false));

        if (Environment.UserInteractive)
            config.WriteTo.Async(a => a.Console());

        return config.CreateLogger();
    }
}
