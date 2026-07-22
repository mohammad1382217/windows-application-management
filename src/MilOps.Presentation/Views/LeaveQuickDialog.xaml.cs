using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

/// <summary>Small modal to record (and immediately approve) a leave for one soldier.</summary>
public partial class LeaveQuickDialog : Window
{
    private readonly LeaveQuickViewModel _vm;
    private readonly IServiceScope _scope;

    public LeaveQuickDialog(int soldierId, string soldierDisplayName, DateTime defaultDate)
    {
        InitializeComponent();
        _scope = App.Services.CreateScope();
        _vm = _scope.ServiceProvider.GetRequiredService<LeaveQuickViewModel>();
        _vm.Initialize(soldierId, soldierDisplayName, defaultDate);
        _vm.Saved += OnSaved;
        _vm.Cancelled += OnCancelled;
        DataContext = _vm;
        Closed += OnClosed;
    }

    private void OnSaved() { DialogResult = true; Close(); }
    private void OnCancelled() { DialogResult = false; Close(); }

    private void OnClosed(object? sender, EventArgs e)
    {
        _vm.Saved -= OnSaved;
        _vm.Cancelled -= OnCancelled;
        Closed -= OnClosed;
        _scope.Dispose();
    }
}
