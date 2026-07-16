using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class SoldiersView : UserControl
{
    public SoldiersView() => InitializeComponent();
    private void SoldiersView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SoldiersViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }

    // Double-click a row to edit it — the near-universal expectation for
    // editable grids, instead of always requiring select-then-click-the-button.
    // Guarded to actual rows so double-clicking a column header (e.g. to sort)
    // doesn't accidentally open the editor for whatever row was last selected.
    private void DataGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject) is null) return;
        if (DataContext is SoldiersViewModel vm && vm.EditCommand.CanExecute(null))
            vm.EditCommand.Execute(null);
    }

    private static T? FindVisualParent<T>(DependencyObject? node) where T : DependencyObject
    {
        while (node is not null)
        {
            if (node is T match) return match;
            node = VisualTreeHelper.GetParent(node);
        }
        return null;
    }
}
