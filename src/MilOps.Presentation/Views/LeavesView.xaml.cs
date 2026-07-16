using System.Windows.Controls;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class LeavesView : UserControl
{
    public LeavesView() => InitializeComponent();
    private void LeavesView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not LeavesViewModel vm) return;
        if (vm.LoadCommand.CanExecute(null)) vm.LoadCommand.Execute(null);
        if (vm.LoadSoldiersCommand.CanExecute(null)) vm.LoadSoldiersCommand.Execute(null);
    }
}
