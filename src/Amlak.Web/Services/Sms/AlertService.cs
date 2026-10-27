using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Sms;

public static class AlertMatcher
{
    public static bool Matches(SavedSearch s, Property p)
    {
        if (!s.Active || s.Deal != p.Deal) return false;
        if (s.Kind is { } kind && kind != p.Kind) return false;
        if (!Same(s.City, p.City)) return false;
        var districts = Districts(s.Districts);
        if (districts.Count > 0 && !districts.Any(d => Same(d, p.District))) return false;
        if (s.MinBedrooms is { } beds && (p.Bedrooms ?? 0) < beds) return false;
        if (s.MaxBudget is { } max && PriceText.Comparable(p) is { } price && price > max) return false;
        return true;
    }

    public static List<string> Districts(string? districts) =>
        (districts ?? "").Split([',', '،'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(PersianText.Normalize).Distinct().ToList();

    // People type the same place with and without spaces or half-spaces ("سعادت آباد", "سعادت‌آباد").
    private static bool Same(string a, string b) => Key(a) == Key(b);
    private static string Key(string s) => PersianText.Normalize(s).Replace("‌", "").Replace(" ", "");
}

/// <summary>
/// Texts subscribers about a listing that matches their saved search: once when it goes live after they
/// signed up, and again whenever its price falls below the lowest price they were told. Runs from the
/// outbox, so it may run twice for one change; <see cref="AlertSent"/> keeps it to one text each.
/// </summary>
public class AlertService(AppDbContext db, SmsGateway sms, IConfiguration cfg, TimeProvider clock)
{
    private readonly string _baseUrl = (cfg["Site:BaseUrl"] ?? "http://localhost").TrimEnd('/');

    /// <returns>How many texts were sent.</returns>
    public async Task<int> NotifyAsync(int propertyId, CancellationToken ct = default)
    {
        var p = await db.Properties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == propertyId, ct);
        if (p is null || p.Status != ListingStatus.Published || PriceText.Comparable(p) is not { } price) return 0;

        var city = p.City;
        var subscribers = (await db.SavedSearches.AsNoTracking()
            .Where(s => s.Active && s.Deal == p.Deal && s.City == city && (s.NotifyNew || s.NotifyPriceDrop))
            .ToListAsync(ct)).Where(s => AlertMatcher.Matches(s, p)).ToList();
        if (subscribers.Count == 0) return 0;
        var told = (await db.AlertsSent.AsNoTracking().Where(a => a.PropertyId == p.Id).ToListAsync(ct))
            .GroupBy(a => a.SavedSearchId).ToDictionary(g => g.Key, g => g.Min(a => a.Price));

        var sent = 0;
        foreach (var s in subscribers)
        {
            string text;
            AlertKind kind;
            if (!told.TryGetValue(s.Id, out var lowest))
            {
                // Listings that were already live when the visitor signed up are not "new" to them.
                if (!s.NotifyNew || p.PublishedAtUtc < s.CreatedAtUtc) continue;
                (kind, text) = (AlertKind.NewListing, $"آگهی تازه مطابق پیگیری شما:\n{p.Title}، {p.District}\n{PriceText.Label(p)}\n{Link(p)}");
            }
            else
            {
                // A hidden ("توافقی") price is never revealed through a price-drop text.
                if (!s.NotifyPriceDrop || !p.ShowsPrice || price >= lowest) continue;
                (kind, text) = (AlertKind.PriceDrop, $"کاهش قیمت: «{p.Title}» از {Fa.Money(lowest)} به {Fa.Money(price)} تومان"
                    + (p.Deal == DealType.Rent ? " (معادل رهن کامل)" : "") + $" رسید.\n{Link(p)}");
            }
            if (!await sms.SendAsync(s.Phone, text, ct: ct)) continue;
            db.AlertsSent.Add(new AlertSent { SavedSearchId = s.Id, PropertyId = p.Id, Kind = kind, Price = price, SentAtUtc = clock.GetUtcNow().UtcDateTime });
            await db.SaveChangesAsync(ct);
            sent++;
        }
        return sent;
    }

    private string Link(Property p) => ShortLink(_baseUrl, p.Id);

    /// <summary>The /p/{id} redirect: short enough not to add a billed SMS part.</summary>
    public static string ShortLink(string baseUrl, int propertyId) => $"{baseUrl.TrimEnd('/')}/p/{propertyId}";
}
