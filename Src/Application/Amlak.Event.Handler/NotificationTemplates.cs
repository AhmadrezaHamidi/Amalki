namespace Amlak.Event.Handler;

/// <summary>متن پیامک‌های اعلان به مدیر</summary>
public static class NotificationTemplates
{
    public static string NewLead(string fullName, string phoneNumber, long? propertyId)
        => propertyId is { } id
            ? $"درخواست تماس جدید برای آگهی {id}:\n{fullName} — {phoneNumber}"
            : $"درخواست تماس جدید:\n{fullName} — {phoneNumber}";
}
