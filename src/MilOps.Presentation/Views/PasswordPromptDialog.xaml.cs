using System.Windows;

namespace MilOps.Presentation.Views;

/// <summary>Masked variant of InputDialog for entering a new password — plain-text
/// entry would be inconsistent with the PasswordBox used on the create-user form.</summary>
public partial class PasswordPromptDialog : Window
{
    private PasswordPromptDialog(string prompt, string title, string hint)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        HintText.Text = hint;
        Loaded += (_, _) => ValueBox.Focus();
    }

    /// <summary>Shows a modal masked prompt; returns null if cancelled.</summary>
    public static string? Prompt(string prompt, string title, string hint = "")
    {
        var owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var dlg = new PasswordPromptDialog(prompt, title, hint);
        if (owner is not null && owner.IsVisible) dlg.Owner = owner;
        else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dlg.ShowDialog() == true ? dlg.ValueBox.Password : null;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
