using System.Windows.Controls;
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
}
