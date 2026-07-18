using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MilOps.Presentation.ViewModels;

namespace MilOps.Presentation.Views;

public partial class ScheduleBuilderWindow : Window
{
    private readonly ScheduleBuilderViewModel _vm;
    private readonly IServiceScope _scope;

    public ScheduleBuilderWindow(DateTime? initialDate = null, int? editScheduleId = null)
    {
        InitializeComponent();
        _scope = App.Services.CreateScope();
        _vm = _scope.ServiceProvider.GetRequiredService<ScheduleBuilderViewModel>();
        if (initialDate.HasValue) _vm.Date = initialDate.Value;
        _vm.EditScheduleId = editScheduleId;
        Title = _vm.WindowTitle;
        _vm.Saved += OnSaved;
        _vm.Cancelled += OnCancelled;
        DataContext = _vm;
        Closed += OnClosed;
    }

    private void ScheduleBuilderWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_vm.InitializeCommand.CanExecute(null))
            _vm.InitializeCommand.Execute(null);
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
