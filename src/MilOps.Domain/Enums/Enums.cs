using System.ComponentModel;

namespace MilOps.Domain.Enums;

public enum Role
{
    [Description("فرمانده")] Commander = 1,
    [Description("اپراتور")] Operator = 2,
    [Description("فقط‌خواندنی")] ReadOnly = 3
}

public enum TokenStatus
{
    [Description("فعال")] Active = 1,
    [Description("استفاده‌شده")] Used = 2,
    [Description("ابطال‌شده")] Revoked = 3,
    [Description("منقضی")] Expired = 4
}

public enum TokenPurpose
{
    [Description("فعال‌سازی حساب")] AccountActivation = 1,
    [Description("ثبت‌نام")] Registration = 2,
    [Description("تخصیص مجوز")] PermissionAssignment = 3
}

public enum HealthType
{
    [Description("سالم")] Fit = 1,
    [Description("محدود")] Limited = 2,
    [Description("معاف")] Restricted = 3
}

public enum WeaponType
{
    [Description("تفنگ")] Rifle = 1,
    [Description("پیستول")] Pistol = 2,
    [Description("مسلسل")] MachineGun = 3,
    [Description("کلاشنیکف")] SubmachineGun = 4,
    [Description("سایر")] Other = 99
}

public enum WeaponStatus
{
    [Description("موجود")] Available = 1,
    [Description("تحویل‌داده‌شده")] Assigned = 2,
    [Description("در تعمیر")] InRepair = 3,
    [Description("اسقاط")] Decommissioned = 4
}

public enum LeaveStatus
{
    [Description("درخواست‌شده")] Requested = 1,
    [Description("تأییدشده")] Approved = 2,
    [Description("ردشده")] Rejected = 3,
    [Description("تکمیل‌شده")] Completed = 4,
    [Description("لغوشده")] Cancelled = 5
}

public enum ShiftNumber
{
    [Description("اول")] First = 1,
    [Description("دوم")] Second = 2,
    [Description("سوم")] Third = 3
}

public enum ScheduleStatus
{
    [Description("پیش‌نویس")] Draft = 1,
    [Description("تأییدشده")] Approved = 2,
    [Description("چاپ‌شده")] Printed = 3
}

public enum GuardPost
{
    [Description("پاس ساعت")] PostHoursPass = 1,
    [Description("چشمه")] Spring = 2,
    [Description("قلعه")] Castle = 3,
    [Description("کارگاه")] Workshop = 4,
    [Description("نگهبان مکانیزه")] MechanizedGuard = 5,
    [Description("نگهبان آماد")] AmadGuard = 6,
    [Description("نگهبان مسلح")] ArmedGuard = 7,
    [Description("نگهبان")] Guard = 8,
    [Description("نگهبان بهداری")] MedicalGuard = 9,
    [Description("آشپزخانه")] Kitchen = 10,
    [Description("دیده‌بان")] Watchman = 11,
    [Description("مسلح‌خانه")] Armament = 12,
    [Description("پناهگاه")] Refuge = 13,
    [Description("مسئول پناهگاه")] ShelterManager = 14,
    [Description("صبحگاه نیروهای مسلح")] ArmedForceMorning = 15
}
