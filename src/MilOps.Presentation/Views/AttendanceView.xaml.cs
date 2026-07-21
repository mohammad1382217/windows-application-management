using System.Windows;
using System.Windows.Controls;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class AttendanceView : UserControl
{
    public AttendanceView() => InitializeComponent();

    private void AttendanceView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is AttendanceViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
