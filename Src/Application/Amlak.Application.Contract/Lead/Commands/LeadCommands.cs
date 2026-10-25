using Amlak.Domain.Lead.Enums;

namespace Lead.Application.Commands;

/// <summary>ثبت درخواست تماس توسط بازدیدکننده (بدون ورود)</summary>
public record SubmitLeadCommand(
    long?   PropertyId,
    string  FullName,
    string  PhoneNumber,
    string? Message) : ICommand<long>;

/// <summary>تغییر وضعیت پیگیری توسط مدیر</summary>
public record ChangeLeadStatusCommand(
    [property: JsonIgnore] long Id,
    LeadStatus Status,
    string?    Note) : ICommand<bool>;
