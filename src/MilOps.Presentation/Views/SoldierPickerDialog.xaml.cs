using System.Collections.Generic;
using System.Linq;
using System.Windows;
using MilOps.Application.Soldiers;

namespace MilOps.Presentation.Views;

/// <summary>
/// Modal "pick a soldier from a list" prompt — replaces free-typed soldier-ID
/// entry anywhere a single soldier must be chosen for a one-off action
/// (e.g. issuing a weapon). For persistent inline forms (leave request,
/// guard schedule rows) an inline ComboBox is used instead of this dialog.
/// </summary>
public partial class SoldierPickerDialog : Window
{
    private SoldierPickerDialog(string prompt, string title, IReadOnlyList<SoldierDto> soldiers)
    {
        InitializeComponent();
        Title = title;
        PromptText.Text = prompt;
        SoldierBox.ItemsSource = soldiers;
        if (soldiers.Count == 0)
        {
            SoldierBox.IsEnabled = false;
            EmptyHint.Visibility = Visibility.Visible;
        }
        Loaded += (_, _) => SoldierBox.Focus();
    }

    /// <summary>Shows a modal soldier picker; returns null if cancelled or nothing was selected.</summary>
    public static SoldierDto? Prompt(string prompt, string title, IReadOnlyList<SoldierDto> soldiers)
    {
        var owner = System.Windows.Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive);
        var dlg = new SoldierPickerDialog(prompt, title, soldiers);
        if (owner is not null && owner.IsVisible) dlg.Owner = owner;
        else dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        return dlg.ShowDialog() == true ? dlg.SoldierBox.SelectedItem as SoldierDto : null;
    }

    private void SoldierBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        => OkButton.IsEnabled = SoldierBox.SelectedItem is not null;

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
