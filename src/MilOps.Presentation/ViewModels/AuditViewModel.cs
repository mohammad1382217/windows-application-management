using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Audit;
using MilOps.Domain.Enums;
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
    public AuditAction? ActionFilter { get => _actionFilter; set { _actionFilter = value; OnPropertyChanged(); } }

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

    [RelayCommand]
    private void Print()
    {
        var doc = _print.BuildTableReport(
            "Audit Log", $"{Items.Count} entries",
            new[] { "Seq", "When (UTC)", "Action", "User", "Entity", "Details", "Hash" },
            Items.Select(e => new[]
            {
                e.Sequence.ToString(), e.OccurredAtUtc.ToString("u"), e.Action.ToString(),
                e.Username ?? "—", $"{e.EntityType}/{e.EntityId ?? "—"}",
                e.Details ?? "—", e.RowHashPreview
            }));
        _print.Print(doc, "Audit Log");
    }
}
