using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Weapons;
using MilOps.Domain.Enums;
using MilOps.Presentation.Common;
using MilOps.Presentation.Services;
using MilOps.Presentation.Views;

namespace MilOps.Presentation.ViewModels;

/// <summary>Weapons module: register weapons, issue/return, print assignment report.</summary>
public sealed partial class WeaponsViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly IPrintService _print;

    public ObservableCollection<WeaponDto> Items { get; } = new();
    public Array WeaponTypes => Enum.GetValues(typeof(WeaponType));

    public string NewWeaponNumber { get; set; } = string.Empty;
    public WeaponType NewWeaponType { get; set; } = WeaponType.Rifle;
    public string NewModel { get; set; } = string.Empty;

    private WeaponDto? _selected;
    public WeaponDto? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); IssueCommand.NotifyCanExecuteChanged(); ReturnCommand.NotifyCanExecuteChanged(); HistoryCommand.NotifyCanExecuteChanged(); }
    }

    public WeaponsViewModel(ISender sender, IDialogService dialogs, IPrintService print)
    { _sender = sender; _dialogs = dialogs; _print = print; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var items = await _sender.Send(new ListWeaponsQuery());
            Items.Clear();
            foreach (var w in items) Items.Add(w);
            PrintAllCommand.NotifyCanExecuteChanged();
            ExportPdfCommand.NotifyCanExecuteChanged();
        });
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewWeaponNumber)) { ErrorMessage = "شماره سلاح الزامی است."; return; }
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new CreateWeaponCommand(NewWeaponNumber, NewWeaponType, NewModel));
            if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            NewWeaponNumber = string.Empty; NewModel = string.Empty;
            OnPropertyChanged(nameof(NewWeaponNumber)); OnPropertyChanged(nameof(NewModel));
            await LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task IssueAsync()
    {
        if (Selected is null) return;
        var soldierIdText = InputDialog.Prompt("شناسه سرباز برای تحویل سلاح:", "تحویل سلاح", "");
        if (soldierIdText is null) return; // user cancelled
        if (!int.TryParse(PersianDate.ToLatinDigits(soldierIdText), out var soldierId) || soldierId <= 0)
        { _dialogs.Warning("شناسه سرباز نامعتبر است."); return; }
        var note = InputDialog.Prompt("یادداشت (اختیاری):", "تحویل سلاح", "") ?? string.Empty;
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new IssueWeaponCommand(Selected.Id, soldierId, note));
            if (!r.IsSuccess) _dialogs.Error(r.Error);
            else await LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task ReturnAsync()
    {
        if (Selected is null) return;
        if (!_dialogs.Confirm($"سلاح «{Selected.WeaponNumber}» بازگردانده شود؟")) return;
        var ammoText = InputDialog.Prompt("تعداد مهمات بازگشتی (اختیاری):", "بازگرداندن سلاح", "0");
        if (ammoText is null) return; // user cancelled
        int.TryParse(PersianDate.ToLatinDigits(ammoText), out var ammo);
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new ReturnWeaponCommand(Selected.Id, ammo, null));
            if (!r.IsSuccess) _dialogs.Error(r.Error);
            else await LoadAsync();
        });
    }

    [RelayCommand(CanExecute = nameof(CanAct))]
    private async Task HistoryAsync()
    {
        if (Selected is null) return;
        await RunAsync(async () =>
        {
            var rows = await _sender.Send(new GetWeaponHistoryQuery(Selected.Id));
            var doc = _print.BuildTableReport(
                $"سابقه تخصیص سلاح {Selected.WeaponNumber}",
                $"{EnumText.Describe(Selected.Type)} · {EnumText.Describe(Selected.Status)}",
                new[] { "شناسه سرباز", "تحویل‌دهنده", "تاریخ تحویل", "تاریخ بازگشت", "مهمات بازگشتی" },
                rows.Select(r => new[]
                {
                    PersianDate.ToPersianDigits(r.SoldierId.ToString()),
                    PersianDate.ToPersianDigits(r.IssuedByUserId.ToString()),
                    PersianDate.ToJalaliDateTime(r.IssuedAtUtc),
                    r.ReturnedAtUtc is { } ret ? PersianDate.ToJalaliDateTime(ret) : "—",
                    r.ReturnedAmmunition is { } ammo ? PersianDate.ToPersianDigits(ammo.ToString()) : "—"
                }));
            _print.Print(doc, "سابقه سلاح");
        });
    }

    [RelayCommand(CanExecute = nameof(CanPrintAll))]
    private void PrintAll()
    {
        try
        {
            _print.Print(BuildReport(), "فهرست تسلیحات");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Print failed in WeaponsViewModel.");
            _dialogs.Error("چاپ انجام نشد. از اتصال و روشن بودن چاپگر اطمینان حاصل کنید.");
        }
    }

    [RelayCommand(CanExecute = nameof(CanPrintAll))]
    private void ExportPdf()
    {
        try
        {
            _print.ExportToPdf(BuildReport(), "فهرست تسلیحات.pdf");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "PDF export failed in WeaponsViewModel.");
            _dialogs.Error("ساخت فایل PDF انجام نشد.");
        }
    }

    private bool CanPrintAll() => Items.Count > 0;

    private System.Windows.Documents.FlowDocument BuildReport()
    {
        return _print.BuildTableReport(
            "فهرست تسلیحات یگان",
            PersianDate.ToPersianDigits($"موجودی اسلحه‌خانه — {Items.Count} قبضه"),
            new[] { "شماره سلاح", "نوع", "وضعیت", "مدل", "تخصیص به" },
            Items.Select(w => new[]
            {
                w.WeaponNumber, EnumText.Describe(w.Type), EnumText.Describe(w.Status),
                w.Model ?? "—",
                w.CurrentlyAssignedSoldierId is { } sid ? PersianDate.ToPersianDigits(sid.ToString()) : "—"
            }));
    }

    private bool CanAct() => Selected is not null;
}
