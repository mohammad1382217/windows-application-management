using System.Windows.Controls;
using System.Windows.Input;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class SchedulesView : UserControl
{
    public SchedulesView() => InitializeComponent();

    private void SchedulesView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is not SchedulesViewModel vm) return;
        if (vm.LoadSchedulesCommand.CanExecute(null)) vm.LoadSchedulesCommand.Execute(null);
    }

    private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is SchedulesViewModel { SelectedSchedule: { } row } vm
            && vm.PreviewCommand.CanExecute(row))
            vm.PreviewCommand.Execute(row);
    }
}
