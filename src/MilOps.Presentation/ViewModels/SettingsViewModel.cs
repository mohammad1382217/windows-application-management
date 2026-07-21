using CommunityToolkit.Mvvm.Input;
using MilOps.Presentation.Services;

namespace MilOps.Presentation.ViewModels;

/// <summary>Local, machine-only app preferences (currently: default PDF/print export folder).</summary>
public sealed partial class SettingsViewModel : ViewModelBase
{
    private readonly IAppSettingsStore _store;
    private readonly IDialogService _dialogs;

    private string? _exportFolder;
    public string? ExportFolder
    {
        get => _exportFolder;
        set { _exportFolder = value; OnPropertyChanged(); }
    }

    public SettingsViewModel(IAppSettingsStore store, IDialogService dialogs)
    {
        _store = store;
        _dialogs = dialogs;
        ExportFolder = _store.Load().ExportFolder;
    }

    [RelayCommand]
    private void BrowseFolder()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog
        {
            InitialDirectory = ExportFolder
        };
        if (dlg.ShowDialog() == true)
            ExportFolder = dlg.FolderName;
    }

    [RelayCommand]
    private void Save()
    {
        _store.Save(new AppSettings(string.IsNullOrWhiteSpace(ExportFolder) ? null : ExportFolder));
        _dialogs.Info("تنظیمات ذخیره شد.");
    }
}
