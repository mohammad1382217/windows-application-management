using System.Collections.ObjectModel;
using System.Windows.Threading;
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
    private readonly DispatcherTimer _searchDebounce;
    public string Search
    {
        get => _search;
        set
        {
            if (_search == value) return;
            _search = value; OnPropertyChanged();
            // Debounce instead of querying per keystroke — feels instant to the
            // user but doesn't hammer the DB, and matches HealthFilter's
            // "changes apply immediately" behavior instead of requiring Enter/click.
            _searchDebounce.Stop();
            _searchDebounce.Start();
        }
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

    // ── Extended filter panel ────────────────────────────────────────────────

    private bool _isFiltersExpanded;
    public bool IsFiltersExpanded
    {
        get => _isFiltersExpanded;
        set { _isFiltersExpanded = value; OnPropertyChanged(); }
    }

    [RelayCommand]
    private void ExpandFilters() => IsFiltersExpanded = !IsFiltersExpanded;

    private readonly DispatcherTimer _rankDeptDebounce;

    private string? _rank;
    public string? Rank
    {
        get => _rank;
        set
        {
            if (_rank == value) return;
            _rank = value; OnPropertyChanged();
            _rankDeptDebounce.Stop(); _rankDeptDebounce.Start();
        }
    }

    private string? _departmentFilter;
    public string? DepartmentFilter
    {
        get => _departmentFilter;
        set
        {
            if (_departmentFilter == value) return;
            _departmentFilter = value; OnPropertyChanged();
            _rankDeptDebounce.Stop(); _rankDeptDebounce.Start();
        }
    }

    /// <summary>Tri-state: null = همه, true = فعال, false = غیرفعال.</summary>
    public IReadOnlyList<KeyValuePair<string, bool?>> ActiveFilterOptions { get; } = new[]
    {
        new KeyValuePair<string, bool?>("همه", null),
        new KeyValuePair<string, bool?>("فعال", true),
        new KeyValuePair<string, bool?>("غیرفعال", false),
    };

    private KeyValuePair<string, bool?> _activeFilterOption;
    public KeyValuePair<string, bool?> ActiveFilterOption
    {
        get => _activeFilterOption;
        set { _activeFilterOption = value; OnPropertyChanged(); _ = LoadAsync(); }
    }

    public enum DateFilterField { EntryDate, ServiceStart, ServiceEnd }

    public IReadOnlyList<KeyValuePair<string, DateFilterField>> DateFilterFieldOptions { get; } = new[]
    {
        new KeyValuePair<string, DateFilterField>("تاریخ ورود", DateFilterField.EntryDate),
        new KeyValuePair<string, DateFilterField>("شروع خدمت", DateFilterField.ServiceStart),
        new KeyValuePair<string, DateFilterField>("پایان خدمت", DateFilterField.ServiceEnd),
    };

    private KeyValuePair<string, DateFilterField> _dateFilterFieldOption = new("تاریخ ورود", DateFilterField.EntryDate);
    public KeyValuePair<string, DateFilterField> DateFilterFieldOption
    {
        get => _dateFilterFieldOption;
        set { _dateFilterFieldOption = value; OnPropertyChanged(); if (DateFrom.HasValue || DateTo.HasValue) _ = LoadAsync(); }
    }

    private DateTime? _dateFrom;
    public DateTime? DateFrom
    {
        get => _dateFrom;
        set { _dateFrom = value; OnPropertyChanged(); _ = LoadAsync(); }
    }

    private DateTime? _dateTo;
    public DateTime? DateTo
    {
        get => _dateTo;
        set { _dateTo = value; OnPropertyChanged(); _ = LoadAsync(); }
    }

    [RelayCommand]
    private void ClearAllFilters()
    {
        _rank = null; _departmentFilter = null;
        OnPropertyChanged(nameof(Rank)); OnPropertyChanged(nameof(DepartmentFilter));
        _healthFilter = null; OnPropertyChanged(nameof(HealthFilter));
        _activeFilterOption = default; OnPropertyChanged(nameof(ActiveFilterOption));
        _dateFrom = null; _dateTo = null;
        OnPropertyChanged(nameof(DateFrom)); OnPropertyChanged(nameof(DateTo));
        Search = string.Empty;
        _ = LoadAsync();
    }

    public SoldiersViewModel(ISender sender, IDialogService dialogs, IPrintService print)
    {
        _sender = sender; _dialogs = dialogs; _print = print;
        _searchDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _searchDebounce.Tick += (_, _) => { _searchDebounce.Stop(); _ = LoadAsync(); };
        _rankDeptDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
        _rankDeptDebounce.Tick += (_, _) => { _rankDeptDebounce.Stop(); _ = LoadAsync(); };
    }

    [RelayCommand]
    private void ClearSearch() => Search = string.Empty;

    private string? _resultCountCaption;
    /// <summary>Shown only when the 200-row cap actually truncated the results,
    /// so the user never mistakes a partial list for "everyone".</summary>
    public string? ResultCountCaption
    {
        get => _resultCountCaption;
        private set { _resultCountCaption = value; OnPropertyChanged(); }
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            DateOnly? entryFrom = null, entryTo = null, startFrom = null, startTo = null, endFrom = null, endTo = null;
            var from = DateFrom is { } df ? DateOnly.FromDateTime(df) : (DateOnly?)null;
            var to = DateTo is { } dt ? DateOnly.FromDateTime(dt) : (DateOnly?)null;
            switch (DateFilterFieldOption.Value)
            {
                case DateFilterField.EntryDate: entryFrom = from; entryTo = to; break;
                case DateFilterField.ServiceStart: startFrom = from; startTo = to; break;
                case DateFilterField.ServiceEnd: endFrom = from; endTo = to; break;
            }

            var filter = new SoldierSearchRequest(
                Search, HealthFilter, ActiveFilterOption.Value, DepartmentFilter, 1, 200,
                Rank, entryFrom, entryTo, startFrom, startTo, endFrom, endTo);
            var result = await _sender.Send(new SearchSoldiersQuery(filter));
            Items.Clear();
            foreach (var s in result.Items) Items.Add(s);
            ResultCountCaption = result.TotalCount > result.Items.Count
                ? PersianDate.ToPersianDigits($"نمایش {result.Items.Count} از {result.TotalCount} نتیجه")
                : null;
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
        var deletedName = $"{Selected.FirstName} {Selected.LastName}";
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new DeleteSoldierCommand(Selected.Id));
            if (!r.IsSuccess) { _dialogs.Error(r.Error); return; }
            await LoadAsync();
            _dialogs.Info($"سرباز «{deletedName}» حذف شد.");
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

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task ExportFullReportAsync()
    {
        if (Selected is null) return;
        await RunFullReportAsync(doc => _print.Print(doc, "گزارش کامل سرباز"));
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private async Task ExportFullReportPdfAsync()
    {
        if (Selected is null) return;
        await RunFullReportAsync(doc => _print.ExportToPdf(doc,
            $"گزارش کامل {Selected!.FirstName} {Selected.LastName}.pdf"));
    }

    private async Task RunFullReportAsync(Action<System.Windows.Documents.FlowDocument> output)
    {
        if (Selected is null) return;
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new GetSoldierFullReportQuery(Selected.Id, null, null));
            if (!r.IsSuccess || r.Value is null) { _dialogs.Error(r.Error ?? "گزارش یافت نشد."); return; }
            output(SoldierFullReportBuilder.Build(_print, r.Value));
        });
    }

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
