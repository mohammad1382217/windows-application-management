using System.Windows;

namespace MilOps.Presentation.Services;

/// <summary>Thin wrapper over WPF message boxes, for testability.</summary>
public interface IDialogService
{
    void Info(string message, string title = "MilOps");
    void Warning(string message, string title = "MilOps");
    void Error(string message, string title = "MilOps");
    bool Confirm(string message, string title = "Confirm");
}

public sealed class DialogService : IDialogService
{
    public void Info(string message, string title = "MilOps") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void Warning(string message, string title = "MilOps") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public void Error(string message, string title = "MilOps") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool Confirm(string message, string title = "Confirm") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question)
            == MessageBoxResult.Yes;
}
