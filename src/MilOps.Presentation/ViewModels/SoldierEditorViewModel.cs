using CommunityToolkit.Mvvm.Input;
using MediatR;
using MilOps.Application.Soldiers;
using MilOps.Domain.Enums;

namespace MilOps.Presentation.ViewModels;

/// <summary>Editor VM backing the SoldierEditorWindow. Create or Update path.</summary>
public sealed partial class SoldierEditorViewModel : ViewModelBase
{
    private readonly ISender _sender;
    private int? _id;

    public event Action? Saved;
    public event Action? Cancelled;

    public Array HealthTypes => Enum.GetValues(typeof(HealthType));

    public bool IsEditMode => _id is not null;
    public string WindowTitle => IsEditMode ? "ویرایش سرباز" : "افزودن سرباز جدید";
    public string? CodeFieldToolTip => IsEditMode ? "کد ملی و کد پرسنلی در حالت ویرایش قابل تغییر نیستند." : null;

    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? FatherName { get; set; }
    public string Rank { get; set; } = string.Empty;
    public string NationalCode { get; set; } = string.Empty;
    public string PersonnelCode { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public HealthType HealthType { get; set; } = HealthType.Fit;
    public DateTime EntryDate { get; set; } = DateTime.Today;
    public DateTime ServiceStartDate { get; set; } = DateTime.Today;
    public DateTime ServiceEndDate { get; set; } = DateTime.Today.AddYears(1);
    public bool IsActive { get; set; } = true;

    public SoldierEditorViewModel(ISender sender) => _sender = sender;

    public void Initialize(SoldierDto? dto)
    {
        if (dto is null) return;
        _id = dto.Id;
        FirstName = dto.FirstName; LastName = dto.LastName; FatherName = dto.FatherName;
        Rank = dto.Rank; NationalCode = dto.NationalCode; PersonnelCode = dto.PersonnelCode;
        DepartmentName = dto.DepartmentName; HealthType = dto.HealthType;
        EntryDate = dto.EntryDate.ToDateTime(TimeOnly.MinValue);
        ServiceStartDate = dto.ServiceStartDate.ToDateTime(TimeOnly.MinValue);
        ServiceEndDate = dto.ServiceEndDate.ToDateTime(TimeOnly.MinValue);
        IsActive = dto.IsActive;
        // Refresh every binding (empty string = "all properties changed").
        OnPropertyChanged(string.Empty);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await RunAsync(async () =>
        {
            var entry = DateOnly.FromDateTime(EntryDate);
            var start = DateOnly.FromDateTime(ServiceStartDate);
            var end = DateOnly.FromDateTime(ServiceEndDate);

            if (_id is int id)
            {
                var r = await _sender.Send(new UpdateSoldierCommand(
                    id, FirstName, LastName, FatherName, Rank, HealthType,
                    entry, start, end, DepartmentName, IsActive));
                if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            }
            else
            {
                var r = await _sender.Send(new CreateSoldierCommand(
                    FirstName, LastName, FatherName, Rank, NationalCode, PersonnelCode,
                    HealthType, entry, start, end, DepartmentName, IsActive));
                if (!r.IsSuccess) { ErrorMessage = r.Error; return; }
            }
            Saved?.Invoke();
        });
    }

    [RelayCommand] private void Cancel() => Cancelled?.Invoke();
}
