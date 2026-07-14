using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Audit;
using MilOps.Domain.Enums;
using MilOps.Presentation.Common;
using MilOps.Presentation.Services;

namespace MilOps.Presentation.ViewModels;

/// <summary>
/// Audit log viewer: query by date/action, and verify the tamper-evident HMAC
/// chain integrity on demand. Read-only.
/// </summary>
public sealed partial class AuditViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly IDialogService _dialogs;
    private readonly IPrintService _print;

    public ObservableCollection<AuditEntryDto> Items { get; } = new();
    public Array Actions => Enum.GetValues(typeof(AuditAction));

    private DateTime? _from;
    public DateTime? From { get => _from; set { _from = value; OnPropertyChanged(); } }
    private DateTime? _to;
    public DateTime? To { get => _to; set { _to = value; OnPropertyChanged(); } }
    private AuditAction? _actionFilter;
    public AuditAction? ActionFilter
    {
        get => _actionFilter;
        set
        {
            if (_actionFilter == value) return;
            _actionFilter = value; OnPropertyChanged();
            ClearActionFilterCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearActionFilter))]
    private void ClearActionFilter() { ActionFilter = null; _ = LoadAsync(); }
    private bool CanClearActionFilter() => ActionFilter is not null;

    public AuditViewModel(ISender sender, IDialogService dialogs, IPrintService print)
    { _sender = sender; _dialogs = dialogs; _print = print; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var items = await _sender.Send(new QueryAuditQuery(From, To, ActionFilter));
            Items.Clear();
            foreach (var e in items) Items.Add(e);
            PrintCommand.NotifyCanExecuteChanged();
        });
    }

    [RelayCommand]
    private async Task VerifyAsync()
    {
        await RunAsync(async () =>
        {
            var r = await _sender.Send(new VerifyAuditChainQuery());
            if (r.IsSuccess) _dialogs.Info(r.Value ?? "OK");
            else _dialogs.Error(r.Error);
        });
    }

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private void Print()
    {
        try
        {
            PrintCore();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Print failed in AuditViewModel.");
            _dialogs.Error("چاپ انجام نشد. از اتصال و روشن بودن چاپگر اطمینان حاصل کنید.");
        }
    }

    private bool CanPrint() => Items.Count > 0;

    private void PrintCore()
    {
        var doc = _print.BuildTableReport(
            "گزارش حسابرسی", PersianDate.ToPersianDigits($"{Items.Count} رویداد"),
            new[] { "ردیف", "زمان", "عملیات", "کاربر", "موجودیت", "شرح", "هش" },
            Items.Select(e => new[]
            {
                PersianDate.ToPersianDigits(e.Sequence.ToString()),
                PersianDate.ToJalaliDateTime(e.OccurredAtUtc), EnumText.Describe(e.Action),
                e.Username ?? "—", $"{e.EntityType}/{e.EntityId ?? "—"}",
                e.Details ?? "—", e.RowHashPreview
            }));
        _print.Print(doc, "گزارش حسابرسی");
    }
}
