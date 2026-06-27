using System.Windows.Controls;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class UsersView : UserControl
{
    public UsersView() => InitializeComponent();
    private void UsersView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
