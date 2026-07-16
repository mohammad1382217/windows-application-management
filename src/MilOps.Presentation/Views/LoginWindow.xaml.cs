using System.ComponentModel;
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
        viewModel.PropertyChanged += OnVmPropertyChanged;
        Loaded += (_, _) => UsernameBox.Focus();
        Closed += (_, _) => viewModel.PropertyChanged -= OnVmPropertyChanged;
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is LoginViewModel vm)
            vm.Password = ((PasswordBox)sender).Password;
    }

    // The VM clears Password after a failed attempt; mirror that in the box so
    // the dots on screen never disagree with what will actually be submitted.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not LoginViewModel vm) return;

        if (e.PropertyName is nameof(LoginViewModel.Password)
            && vm.Password.Length == 0 && PasswordBox.Password.Length > 0)
        {
            PasswordBox.Clear();
            PasswordBox.Focus();
        }

        // The activation-token field appears dynamically after a failed login
        // on an unactivated account; move focus into it so the user notices
        // the new required field instead of it silently appearing below.
        if (e.PropertyName is nameof(LoginViewModel.IsActivationRequired) && vm.IsActivationRequired)
            Dispatcher.BeginInvoke(() => ActivationTokenBox.Focus());
    }
}
