using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Tokens;
using MilOps.Domain.Enums;
using MilOps.Presentation.Services;

namespace MilOps.Presentation.ViewModels;

/// <summary>
/// Commander Tokens module: generate (with one-time plaintext reveal), revoke,
/// and list active/expired tokens. The generated plaintext is shown ONCE in a
/// modal and the user is prompted to copy it; it is never stored.
/// </summary>
public sealed partial class TokensViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private readonly IDialogService _dialogs;

    public ObservableCollection<TokenListItemDto> Items { get; } = new();
    public Array Purposes => Enum.GetValues(typeof(TokenPurpose));
    public Array Statuses => Enum.GetValues(typeof(TokenStatus));

    // Generation form
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string NationalCode { get; set; } = string.Empty;
    public string PersonnelCode { get; set; } = string.Empty;
    public string Rank { get; set; } = string.Empty;
    // Nullable: the date pickers can be cleared by the user; Generate validates.
    public DateTime? ServiceStart { get; set; } = DateTime.Today;
    public DateTime? ServiceEnd { get; set; } = DateTime.Today.AddYears(1);
    public TokenPurpose Purpose { get; set; } = TokenPurpose.AccountActivation;
    public int ValidDays { get; set; } = 7;

    private TokenStatus? _statusFilter = TokenStatus.Active;
    public TokenStatus? StatusFilter
    {
        get => _statusFilter;
        set
        {
            if (_statusFilter == value) return;
            _statusFilter = value; OnPropertyChanged();
            _ = LoadAsync(); // filter changes apply immediately
        }
    }

    private TokenListItemDto? _selected;
    public TokenListItemDto? Selected
    {
        get => _selected;
        set { _selected = value; OnPropertyChanged(); RevokeCommand.NotifyCanExecuteChanged(); }
    }

    public TokensViewModel(ISender sender, IDialogService dialogs)
    { _sender = sender; _dialogs = dialogs; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var items = await _sender.Send(new ListTokensQuery(StatusFilter));
            Items.Clear();
            foreach (var t in items) Items.Add(t);
        });
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        await RunAsync(async () =>
        {
            if (ServiceStart is null || ServiceEnd is null)
            {
                ErrorMessage = "تاریخ شروع و پایان خدمت را وارد کنید.";
                return;
            }

            var cmd = new GenerateTokenCommand(
                FirstName, LastName, NationalCode, PersonnelCode, Rank,
                DateOnly.FromDateTime(ServiceStart.Value), DateOnly.FromDateTime(ServiceEnd.Value),
                Purpose, ValidDays);
            var r = await _sender.Send(cmd);
            if (!r.IsSuccess || r.Value is null) { ErrorMessage = r.Error; return; }

            // Show the plaintext EXACTLY ONCE. It cannot be retrieved again, so
            // the reveal dialog must never be skipped — a locked clipboard is
            // reported inside the dialog instead of aborting before it.
            bool copied;
            try { Clipboard.SetText(r.Value.PlaintextToken); copied = true; }
            catch (Exception ex)
            {
                copied = false;
                Serilog.Log.Warning(ex, "Clipboard unavailable while revealing token.");
            }
            // The token is the one piece of text the user must actually find and
            // copy in this dialog; fence it off from the surrounding sentence so
            // it doesn't get lost if the clipboard copy silently failed.
            _dialogs.Info(
                "توکن ایجاد شد — آن را در جای امنی ذخیره کنید.\n\n" +
                $"دارنده: {r.Value.FirstName} {r.Value.LastName}\n" +
                $"هدف: {Common.EnumText.Describe(r.Value.Purpose)}\n" +
                $"انقضا: {Common.PersianDate.ToJalaliDateTime(r.Value.ExpiresAtUtc)}\n\n" +
                (copied ? "(توکن در کلیپ‌بورد کپی شد)" : "(کپی خودکار ناموفق بود — همین حالا آن را یادداشت کنید)") +
                "\n——————————————————\n" +
                r.Value.PlaintextToken +
                "\n——————————————————\n\n" +
                "این توکن دیگر نمایش داده نخواهد شد. فقط نسخه هش‌شده ذخیره می‌شود.",
                "توکن ایجاد شد");
            await LoadAsync();
        }, "Generating token...");
    }

    [RelayCommand(CanExecute = nameof(CanRevoke))]
    private async Task RevokeAsync()
    {
        if (Selected is null) return;
        // Irreversible action: the previous flow had no confirm step, and the
        // reason box started pre-filled with IsDefault="True" on OK — a single
        // stray Enter press revoked the token instantly with no "are you sure".
        if (!_dialogs.Confirm(
            $"توکن «{Selected.Preview}» ابطال شود؟ این عملیات را نمی‌توان بازگرداند.")) return;
        var reason = InputDialog.Prompt("دلیل ابطال این توکن؟", "ابطال توکن", "");
        if (string.IsNullOrWhiteSpace(reason)) return;

        await RunAsync(async () =>
        {
            var r = await _sender.Send(new RevokeTokenCommand(Selected.Id, reason));
            if (!r.IsSuccess) _dialogs.Error(r.Error);
            else await LoadAsync();
        });
    }

    private bool CanRevoke() => Selected is not null && Selected.Status == TokenStatus.Active;
}
