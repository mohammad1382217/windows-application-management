using System.ComponentModel;

namespace MilOps.Domain.Enums;

public enum AuditAction
{
    [Description("ورود")] Login = 1,
    [Description("خروج")] Logout = 2,
    [Description("ورود ناموفق")] LoginFailed = 3,
    [Description("ایجاد کاربر")] UserCreated = 4,
    [Description("ویرایش کاربر")] UserUpdated = 5,
    [Description("غیرفعال‌سازی کاربر")] UserDeactivated = 6,
    [Description("تغییر گذرواژه")] PasswordChanged = 7,
    [Description("صدور توکن")] TokenGenerated = 8,
    [Description("ابطال توکن")] TokenRevoked = 9,
    [Description("استفاده از توکن")] TokenUsed = 10,
    [Description("ایجاد سرباز")] SoldierCreated = 11,
    [Description("ویرایش سرباز")] SoldierUpdated = 12,
    [Description("حذف سرباز")] SoldierDeleted = 13,
    [Description("ایجاد برنامه")] ScheduleCreated = 14,
    [Description("ویرایش برنامه")] ScheduleUpdated = 15,
    [Description("تأیید برنامه")] ScheduleApproved = 16,
    [Description("ثبت دفتر")] RegisterEntryCreated = 17,
    [Description("تحویل سلاح")] WeaponIssued = 18,
    [Description("بازگشت سلاح")] WeaponReturned = 19,
    [Description("ایجاد مرخصی")] LeaveCreated = 20,
    [Description("تأیید مرخصی")] LeaveApproved = 21,
    [Description("رد مرخصی")] LeaveRejected = 22,
    [Description("چاپ گزارش")] ReportPrinted = 23,
    [Description("صدور داده")] DataExported = 24,
    [Description("تغییر تنظیمات")] ConfigChanged = 25,
    [Description("باز کردن قفل پایگاه داده")] DbUnlocked = 26,
    [Description("سایر")] Other = 99
}
