namespace Amlak.Domain.Identity.Enums;

/// <summary>نقش کاربر در سامانه</summary>
public enum UserRole
{
    /// <summary>مدیر املاک — ثبت و انتشار آگهی، پیگیری درخواست‌های تماس</summary>
    Admin = 1,

    /// <summary>کاربر عادی (مشتری)</summary>
    Customer = 2
}

/// <summary>وضعیت کاربر</summary>
public enum UserStatus
{
    Active = 1,
    Inactive = 2,
    Suspended = 3
}
