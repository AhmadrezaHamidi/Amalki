namespace Amlak.Web.Domain;

/// <summary>A visitor's "tell me by SMS" request: new matching listings and price drops are texted to <see cref="Phone"/>.</summary>
public class SavedSearch
{
    public int Id { get; set; }
    /// <summary>Verified mobile, 09xxxxxxxxx.</summary>
    public string Phone { get; set; } = "";
    public string? Name { get; set; }
    public DealType Deal { get; set; }
    public PropertyKind? Kind { get; set; }
    public string City { get; set; } = "";
    /// <summary>Comma-separated district names; empty means the whole city.</summary>
    public string Districts { get; set; } = "";
    /// <summary>Compared with <see cref="Services.PriceText.Comparable"/>: sale price, rent's full-deposit equivalent, nightly price. Toman.</summary>
    public long? MaxBudget { get; set; }
    public int? MinBedrooms { get; set; }
    public bool NotifyNew { get; set; } = true;
    public bool NotifyPriceDrop { get; set; } = true;
    public bool Active { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum AlertKind { NewListing = 1, PriceDrop = 2 }

/// <summary>An alert already texted, so a subscriber never gets the same listing at the same price twice.</summary>
public class AlertSent
{
    public int Id { get; set; }
    public int SavedSearchId { get; set; }
    public int PropertyId { get; set; }
    public AlertKind Kind { get; set; }
    /// <summary>The comparable price the text was about; a later drop is measured from the lowest one sent.</summary>
    public long Price { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>A number that unsubscribed; it gets nothing but one-time codes.</summary>
public class SmsOptOut
{
    public string Phone { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>One alert or campaign text (one-time codes are not logged).</summary>
public class SmsLog
{
    public int Id { get; set; }
    public string Phone { get; set; } = "";
    public string Text { get; set; } = "";
    public int? CampaignId { get; set; }
    public bool Ok { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}

public enum CampaignAudience { Subscribers = 1, Uploaded = 2 }
public enum CampaignStatus { Scheduled = 0, Sending = 1, Done = 2, Cancelled = 3 }

/// <summary>A bulk SMS from the admin panel, sent now or at a scheduled time.</summary>
public class SmsCampaign
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Text with placeholders {نام} {عنوان} {محله} {قیمت} {لینک}; the opt-out footer is added on send.</summary>
    public string Template { get; set; } = "";
    public CampaignAudience Audience { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public DealType? Deal { get; set; }
    /// <summary>Numbers, one per line, when <see cref="Audience"/> is Uploaded.</summary>
    public string? UploadedPhones { get; set; }
    /// <summary>Listing whose title, price and link fill the placeholders, if any.</summary>
    public int? PropertyId { get; set; }
    public DateTime ScheduledAtUtc { get; set; } = DateTime.UtcNow;
    public CampaignStatus Status { get; set; } = CampaignStatus.Scheduled;
    public int Recipients { get; set; }
    public int Sent { get; set; }
    public int Failed { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
