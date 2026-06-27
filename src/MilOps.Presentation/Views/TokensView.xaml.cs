using System.Windows.Controls;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class TokensView : UserControl
{
    public TokensView() => InitializeComponent();
    private void TokensView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is TokensViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
