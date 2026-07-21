using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace MilOps.Presentation.Services;

/// <summary>Local, machine-only app preferences (not sensitive, not synced, not audited).</summary>
public sealed record AppSettings(string? ExportFolder);

/// <summary>
/// Persists app-wide local preferences (currently: the default folder for
/// PDF/print exports) as plain JSON under %LocalAppData%\MilOps\. Mirrors
/// SessionTokenStore's directory convention but skips DPAPI — a folder path
/// is not sensitive.
/// </summary>
public interface IAppSettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}

public sealed class AppSettingsStore : IAppSettingsStore
{
    private readonly ILogger<AppSettingsStore> _logger;
    private readonly string _path;

    public AppSettingsStore(ILogger<AppSettingsStore> logger)
    {
        _logger = logger;
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MilOps");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path)) return new AppSettings(null);
        try
        {
            var json = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings(null);
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Fail closed to defaults rather than blocking the app over a preferences file.
            _logger.LogWarning(ex, "Stored app settings unreadable; using defaults.");
            return new AppSettings(null);
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(settings));
        }
        catch (IOException ex)
        {
            // Persistence is a convenience; never block the primary action over it.
            _logger.LogWarning(ex, "Failed to persist app settings.");
        }
    }
}
