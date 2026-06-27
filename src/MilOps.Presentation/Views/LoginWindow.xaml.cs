using System.Windows;
using System.Windows.Controls;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

/// <summary>
/// Login window. WPF PasswordBox does not expose a bindable Password property
/// by design (to avoid keeping the plaintext in memory via binding), so we
/// forward it to the VM manually on each change and clear it after use.
/// </summary>
public partial class LoginWindow : Window
{
    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => UsernameBox.Focus();
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.Password = ((PasswordBox)sender).Password;
    }
}
