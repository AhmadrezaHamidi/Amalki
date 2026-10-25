namespace Amlak.Web.Domain;

/// <summary>How the asking price is shown on the site, channels and SMS.</summary>
public enum PriceDisplay { Full = 0, TotalOnly = 1, Negotiable = 2 }

/// <summary>A visitor's "tell me by SMS" request: new matching listings and price drops are texted to <see cref="Phone"/>.</summary>
public class SavedSearch
{
    public int Id { get; set; }
    /// <summary>Normalized mobile number, 09xxxxxxxxx.</summary>
    public string Phone { get; set; } = "";
    public string? Name { get; set; }
    public DealType Deal { get; set; }
    public PropertyKind? Kind { get; set; }
    public string City { get; set; } = "";
    /// <summary>Comma-separated district names; empty means the whole city.</summary>
    public string Districts { get; set; } = "";
    /// <summary>Sale: total price. Rent: full-deposit equivalent (see <see cref="Services.Pricing.FullDeposit"/>). Toman.</summary>
    public long? MaxBudget { get; set; }
    public int? MinBedrooms { get; set; }
    public bool NotifyNew { get; set; } = true;
    public bool NotifyPriceDrop { get; set; } = true;
    public bool Active { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum AlertKind { NewListing = 1, PriceDrop = 2 }

/// <summary>One alert already texted, so the same listing (at the same price) is never sent twice to a subscriber.</summary>
public class AlertSent
{
    public int Id { get; set; }
    public int SavedSearchId { get; set; }
    public int PropertyId { get; set; }
    public AlertKind Kind { get; set; }
    public long Price { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Numbers that replied «لغو۱۱» or unsubscribed; nothing but OTP codes is sent to them.</summary>
public class SmsOptOut
{
    public string Phone { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}

public class SmsLog
{
    public int Id { get; set; }
    public string Phone { get; set; } = "";
    public string Text { get; set; } = "";
    public int? CampaignId { get; set; }
    public bool Ok { get; set; }
    public string? Error { get; set; }
    public string? ProviderMessageId { get; set; }
    public DateTime SentAtUtc { get; set; } = DateTime.UtcNow;
}

public enum CampaignAudience { Subscribers = 1, Uploaded = 2 }
public enum CampaignStatus { Scheduled = 0, Sending = 1, Done = 2, Cancelled = 3 }

/// <summary>A bulk SMS send from the admin panel, optionally scheduled.</summary>
public class SmsCampaign
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    /// <summary>Text with placeholders {نام} {عنوان} {محله} {قیمت} {لینک}; the opt-out footer is appended on send.</summary>
    public string Template { get; set; } = "";
    public CampaignAudience Audience { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public DealType? Deal { get; set; }
    /// <summary>Uploaded numbers (one per line) when <see cref="Audience"/> is Uploaded.</summary>
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

/// <summary>A one-time login/verification code sent by SMS. Only a hash of the code is stored.</summary>
public class PhoneOtp
{
    public int Id { get; set; }
    public string Phone { get; set; } = "";
    public string CodeHash { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; }
    public int Attempts { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
}
