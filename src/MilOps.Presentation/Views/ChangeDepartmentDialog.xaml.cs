using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

/// <summary>Small modal to change one soldier's department. Own DI scope, same pattern as SoldierEditorWindow.</summary>
public partial class ChangeDepartmentDialog : Window
{
    private readonly ChangeDepartmentViewModel _vm;
    private readonly IServiceScope _scope;

    /// <summary>The department name that was saved (valid once DialogResult == true).</summary>
    public string NewDepartmentValue => _vm.NewDepartment;

    public ChangeDepartmentDialog(int soldierId, string soldierDisplayName, string currentDepartment)
    {
        InitializeComponent();
        _scope = App.Services.CreateScope();
        _vm = _scope.ServiceProvider.GetRequiredService<ChangeDepartmentViewModel>();
        _vm.Initialize(soldierId, soldierDisplayName, currentDepartment);
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
