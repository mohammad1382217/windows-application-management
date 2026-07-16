using System.Windows.Controls;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class WeaponsView : UserControl
{
    public WeaponsView() => InitializeComponent();
    private void WeaponsView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not WeaponsViewModel vm) return;
        if (vm.LoadCommand.CanExecute(null)) vm.LoadCommand.Execute(null);
        if (vm.LoadSoldiersCommand.CanExecute(null)) vm.LoadSoldiersCommand.Execute(null);
    }
}
