using System.Windows.Controls;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class AuditView : UserControl
{
    public AuditView() => InitializeComponent();
    private void AuditView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is AuditViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }
}
