using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Sms;

public static class SmsText
{
    /// <summary>Footer required on promotional SMS in Iran; operators unsubscribe numbers that reply with it.</summary>
    public const string OptOutFooter = "لغو۱۱";

    /// <summary>Fills {placeholders}; unknown ones are left as they are, so a typo shows in the preview.</summary>
    public static string Render(string template, IReadOnlyDictionary<string, string?> values)
    {
        var text = template;
        foreach (var (key, value) in values) text = text.Replace("{" + key + "}", value ?? "");
        return text.Trim();
    }

    public static string WithOptOut(string text) =>
        text.Contains(OptOutFooter) ? text : text.TrimEnd() + "\n" + OptOutFooter;

    /// <summary>
    /// Billable parts. Persian text is sent as UCS-2: 70 characters in one part, 67 per part when longer.
    /// Plain ASCII (GSM-7): 160 / 153.
    /// </summary>
    public static int Segments(string text)
    {
        if (text.Length == 0) return 0;
        var (single, multi) = text.Any(c => c > 127) ? (70, 67) : (160, 153);
        return text.Length <= single ? 1 : (int)Math.Ceiling(text.Length / (double)multi);
    }
}

/// <summary>
/// Alerts and campaigns go through here: opted-out numbers are skipped, the opt-out footer is added and
/// each attempt is written to <see cref="SmsLog"/>. One-time codes go straight to <see cref="ISmsSender"/>.
/// </summary>
public class SmsGateway(AppDbContext db, ISmsSender sender, TimeProvider clock)
{
    /// <returns>False when the number is invalid or opted out, or the provider refused the message.</returns>
    public async Task<bool> SendAsync(string phone, string text, int? campaignId = null, CancellationToken ct = default)
    {
        if (Fa.Mobile(phone) is not { } mobile || await IsOptedOutAsync(mobile, ct)) return false;
        text = SmsText.WithOptOut(text);
        var ok = await sender.SendTextAsync(mobile, text, ct);
        db.SmsLogs.Add(new SmsLog
        {
            Phone = mobile, Text = text.Length <= 1000 ? text : text[..1000], CampaignId = campaignId, Ok = ok,
            SentAtUtc = clock.GetUtcNow().UtcDateTime,
        });
        await db.SaveChangesAsync(ct);
        return ok;
    }

    public Task<bool> IsOptedOutAsync(string mobile, CancellationToken ct = default) =>
        db.SmsOptOuts.AnyAsync(x => x.Phone == mobile, ct);

    /// <summary>Stops every alert and campaign to a number, and ends its saved searches.</summary>
    public async Task OptOutAsync(string mobile, CancellationToken ct = default)
    {
        if (!await IsOptedOutAsync(mobile, ct)) db.SmsOptOuts.Add(new SmsOptOut { Phone = mobile, AtUtc = clock.GetUtcNow().UtcDateTime });
        foreach (var s in await db.SavedSearches.Where(x => x.Phone == mobile && x.Active).ToListAsync(ct)) s.Active = false;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>A new, confirmed sign-up is fresh consent, so an earlier opt-out no longer applies.</summary>
    public async Task OptInAsync(string mobile, CancellationToken ct = default)
    {
        if (await db.SmsOptOuts.FirstOrDefaultAsync(x => x.Phone == mobile, ct) is { } row) db.SmsOptOuts.Remove(row);
    }
}
