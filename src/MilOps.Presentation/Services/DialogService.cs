using System.Windows;

namespace MilOps.Presentation.Services;

/// <summary>Thin wrapper over WPF message boxes, for testability.</summary>
public interface IDialogService
{
    void Info(string message, string title = "سنگر");
    void Warning(string message, string title = "هشدار");
    void Error(string message, string title = "خطا");
    bool Confirm(string message, string title = "تأیید");
}

public sealed class DialogService : IDialogService
{
    // RTL layout + right-aligned text so Persian messages render correctly.
    private const MessageBoxOptions Rtl = MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign;

    public void Info(string message, string title = "سنگر") =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Warning(string message, string title = "هشدار") =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void Error(string message, string title = "خطا") =>
        Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title = "تأیید") =>
        Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;

    private static MessageBoxResult Show(string message, string title,
        MessageBoxButton buttons, MessageBoxImage image)
    {
        var owner = System.Windows.Application.Current?.Windows
                        .OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? System.Windows.Application.Current?.MainWindow;
        return owner is { IsLoaded: true }
            ? MessageBox.Show(owner, message, title, buttons, image, MessageBoxResult.None, Rtl)
            : MessageBox.Show(message, title, buttons, image, MessageBoxResult.None, Rtl);
    }
}
