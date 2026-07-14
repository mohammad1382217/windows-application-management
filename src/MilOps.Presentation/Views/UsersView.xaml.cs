using System.ComponentModel;
using System.Windows.Controls;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is UsersViewModel oldVm) oldVm.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is UsersViewModel newVm) newVm.PropertyChanged += OnVmPropertyChanged;
        };
    }

    private void UsersView_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel vm && vm.LoadCommand.CanExecute(null))
            vm.LoadCommand.Execute(null);
    }

    // PasswordBox has no bindable Password property (by design); forward manually.
    private void NewPasswordBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is UsersViewModel vm)
            vm.NewPassword = ((PasswordBox)sender).Password;
    }

    // The VM clears NewPassword after a successful create; mirror it in the box.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not nameof(UsersViewModel.NewPassword) || sender is not UsersViewModel vm) return;
        if (vm.NewPassword.Length == 0 && NewPasswordBox.Password.Length > 0)
            NewPasswordBox.Clear();
    }
}
