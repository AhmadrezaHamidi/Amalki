using AhmadBase.Doamin;

namespace Amlak.Domain.Events.Lead;

/// <summary>بازدیدکننده درخواست تماس ثبت کرد — باید به مدیر اطلاع داده شود</summary>
public sealed record LeadSubmittedEvent(long LeadId, long? PropertyId, string FullName, string PhoneNumber) : IEvent;

/// <summary>وضعیت پیگیری درخواست تماس تغییر کرد</summary>
public sealed record LeadStatusChangedEvent(long LeadId, string Status) : IEvent;
