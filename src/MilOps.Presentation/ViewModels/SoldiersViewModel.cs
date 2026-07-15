using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Soldiers;
using MilOps.Domain.Enums;
using MilOps.Presentation.Common;
using MilOps.Presentation.Services;

namespace MilOps.Presentation.ViewModels;

/// <summary>
/// Soldiers module: search/filter, create/update/delete, print the soldier list.
/// Commands are routed through MediatR to the Application layer; the VM never
/// touches DbContext directly. Create/edit go through a small inline editor.
/// </summary>
public sealed partial class SoldiersViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly IPrintService _print;

    public ObservableCollection<SoldierDto> Items { get; } = new();

    private string _search = string.Empty;
    public string Search
    {
        get => _search;
        set { _search = value; OnPropertyChanged(); }
    }

    private SoldierDto? _selected;
    public SoldierDto? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); EditCommand.NotifyCanExecuteChanged(); DeleteCommand.NotifyCanExecuteChanged(); }
    }

    public Array HealthTypes => Enum.GetValues(typeof(HealthType));
    private HealthType? _healthFilter;
    public HealthType? HealthFilter
    {
        get => _healthFilter;
        set
        {
            if (_healthFilter == value) return;
            _healthFilter = value; OnPropertyChanged();
            ClearHealthFilterCommand.NotifyCanExecuteChanged();
            _ = LoadAsync(); // filter changes apply immediately
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearHealthFilter))]
    private void ClearHealthFilter() => HealthFilter = null;
    private bool CanClearHealthFilter() => HealthFilter is not null;

    public SoldiersViewModel(ISender sender, IDialogService dialogs, IPrintService print)
    { _sender = sender; _dialogs = dialogs; _print = print; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var filter = new SoldierSearchRequest(Search, HealthFilter, null, null, 1, 200);
            var result = await _sender.Send(new SearchSoldiersQuery(filter));
            Items.Clear();
            foreach (var s in result.Items) Items.Add(s);
            PrintCommand.NotifyCanExecuteChanged();
            ExportPdfCommand.NotifyCanExecuteChanged();
        });
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit()
    {
        if (Selected is null) return;
        var editor = new SoldierEditorWindow(Selected) { Owner = System.Windows.Application.Current.MainWindow };
        if (editor.ShowDialog() == true)
        {
            // Editor applied changes via its own command pipeline; refresh list.
            _ = LoadAsync();
        }
    }

    private bool CanEdit() => Selected is not null;

    [RelayCommand]
    private void Add()
    {
        var editor = new SoldierEditorWindow(null) { Owner = System.Windows.Application.Current.MainWindow };
        if (editor.ShowDialog() == true) _ = LoadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task DeleteAsync()
    {
        if (Selected is null) return;
        if (!_dialogs.Confirm($"سرباز «{Selected.FirstName} {Selected.LastName}» حذف شود؟ این عملیات در گزارش حسابرسی ثبت می‌شود.")) return;
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new DeleteSoldierCommand(Selected.Id));
            if (!r.IsSuccess) _dialogs.Error(r.Error);
            else await LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void Print()
    {
        try
        {
            _print.Print(BuildReport(), "لیست سربازان");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Print failed in SoldiersViewModel.");
            _dialogs.Error("چاپ انجام نشد. از اتصال و روشن بودن چاپگر اطمینان حاصل کنید.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void ExportPdf()
    {
        try
        {
            _print.ExportToPdf(BuildReport(), "لیست سربازان.pdf");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "PDF export failed in SoldiersViewModel.");
            _dialogs.Error("ساخت فایل PDF انجام نشد.");
        }
    }

    private bool CanPrint() => Items.Count > 0;

    private System.Windows.Documents.FlowDocument BuildReport()
    {
        return _print.BuildTableReport(
            "لیست سربازان",
            PersianDate.ToPersianDigits($"پرسنل یگان — {Items.Count} نفر"),
            new[] { "کد پرسنلی", "نام", "نام خانوادگی", "درجه", "کد ملی", "یگان/بخش", "سلامت", "فعال" },
            Items.Select(s => new[]
            {
                PersianDate.ToPersianDigits(s.PersonnelCode), s.FirstName, s.LastName, s.Rank,
                PersianDate.ToPersianDigits(s.NationalCode), s.DepartmentName,
                EnumText.Describe(s.HealthType), s.IsActive ? "بله" : "خیر"
            }));
    }
}
