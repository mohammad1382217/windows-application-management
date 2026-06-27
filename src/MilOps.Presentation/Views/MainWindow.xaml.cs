using System.Windows;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
