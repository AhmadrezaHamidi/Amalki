using Amlak.Domain.Lead.Args;
using Amlak.Domain.Lead.Enums;
using Amlak.Domain.Lead.Exceptions;

namespace Amlak.Domain.Lead.Aggregates;

/// <summary>
/// Aggregate Root درخواست تماس (سرنخ) — بازدیدکننده برای یک آگهی یا به‌طور کلی شماره می‌گذارد
/// و مدیر آن را پیگیری می‌کند. New → Contacted → Closed؛ Closed و Spam پایانی‌اند.
/// </summary>
public sealed class ContactLead : AggregateRoot<long>
{
    /// <summary>حداکثر درخواست از یک شماره در <see cref="RateWindow"/> — جلوی پر شدن صندوق با ربات</summary>
    public const int MaxPerPhoneInWindow = 3;
    public static readonly TimeSpan RateWindow = TimeSpan.FromMinutes(10);

    public long?      PropertyId  { get; private set; }
    public string     FullName    { get; private set; } = string.Empty;
    public string     PhoneNumber { get; private set; } = string.Empty;
    public string?    Message     { get; private set; }
    public LeadStatus Status      { get; private set; }
    public string?    AdminNote   { get; private set; }
    public DateTime   CreatedAt   { get; private set; }
    public DateTime?  HandledAt   { get; private set; }

    private ContactLead() { }

    private ContactLead(SubmitLeadArg arg) : base(arg.Id)
    {
        PropertyId  = arg.PropertyId;
        FullName    = arg.FullName.Trim();
        PhoneNumber = arg.PhoneNumber.Trim();
        Message     = string.IsNullOrWhiteSpace(arg.Message) ? null : arg.Message.Trim();
        Status      = LeadStatus.New;
        CreatedAt   = DateTime.UtcNow;
    }

    public static ContactLead Submit(SubmitLeadArg arg)
    {
        var lead = new ContactLead(arg);
        lead.RaiseDomainEvent(new LeadSubmittedEvent(arg.Id, arg.PropertyId, lead.FullName, lead.PhoneNumber));
        return lead;
    }

    public void ChangeStatus(LeadStatus status, string? note)
    {
        if (!CanMoveTo(status)) throw new InvalidLeadTransitionException();

        Status    = status;
        HandledAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(note)) AdminNote = note.Trim();
        RaiseDomainEvent(new LeadStatusChangedEvent(Id, status.ToString()));
    }

    private bool CanMoveTo(LeadStatus target) => (Status, target) switch
    {
        (LeadStatus.New,       LeadStatus.Contacted or LeadStatus.Closed or LeadStatus.Spam) => true,
        (LeadStatus.Contacted, LeadStatus.Closed or LeadStatus.Spam)                         => true,
        _                                                                                    => false
    };
}
