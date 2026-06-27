using System.Windows.Controls;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class SchedulesView : UserControl
{
    public SchedulesView() => InitializeComponent();
    private void SchedulesView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is SchedulesViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
