using System.Windows;

namespace MilOps.Presentation.Views;

/// <summary>A minimal single-line input prompt (avoids VB Interaction.InputBox).</summary>
public partial class InputDialog : Window
{
    private InputDialog(string prompt, string title, string defaultValue)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        ValueBox.Text = defaultValue;
        Loaded += (_, _) => { ValueBox.Focus(); ValueBox.SelectAll(); };
    }

    /// <summary>Shows a modal prompt; returns null if cancelled.</summary>
    public static string? Prompt(string prompt, string title, string defaultValue = "")
    {
        var owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var dlg = new InputDialog(prompt, title, defaultValue);
        if (owner is not null && owner.IsVisible) dlg.Owner = owner;
        else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dlg.ShowDialog() == true ? dlg.ValueBox.Text : null;
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
