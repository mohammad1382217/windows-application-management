using System;
using System.ComponentModel;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MilOps.Application.Soldiers;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

/// <summary>
/// Modal soldier create/edit form. Receives the existing DTO (edit) or null (create)
/// and runs on its own DI scope. The scope (and its DbContext) is disposed when
/// the window closes, and the Saved/Cancelled event hooks are removed to avoid
/// pinning the VM/window alive.
/// </summary>
public partial class SoldierEditorWindow : Window
{
    private readonly SoldierEditorViewModel _vm;
    private readonly IServiceScope _scope;

    public SoldierEditorWindow(SoldierDto? existing)
    {
        InitializeComponent();
        _scope = App.Services.CreateScope();
        _vm = _scope.ServiceProvider.GetRequiredService<SoldierEditorViewModel>();
        _vm.Initialize(existing);
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
