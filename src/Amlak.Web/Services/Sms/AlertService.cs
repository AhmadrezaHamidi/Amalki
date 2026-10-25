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
        var districts = SplitDistricts(s.Districts);
        if (districts.Count > 0 && !districts.Any(d => Same(d, p.District))) return false;
        if (s.MinBedrooms is { } beds && (p.Bedrooms ?? 0) < beds) return false;
        if (s.MaxBudget is { } max && Pricing.Effective(p) is { } price && price > max) return false;
        return true;
    }

    public static List<string> SplitDistricts(string districts) =>
        districts.Split([',', '،'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static bool Same(string a, string b) =>
        string.Equals(Clean(a), Clean(b), StringComparison.OrdinalIgnoreCase);

    // Arabic/Persian letter variants and zero-width non-joiners vary between typists.
    private static string Clean(string s) =>
        s.Trim().Replace('ي', 'ی').Replace('ك', 'ک').Replace("‌", "").Replace(" ", "");
}

/// <summary>
/// Texts subscribers when a listing that matches their saved search goes live, and again
/// when its price drops. Each (subscriber, listing, kind, price) is sent at most once.
/// </summary>
public class AlertService(AppDbContext db, SmsGateway sms, IConfiguration cfg)
{
    private readonly string _baseUrl = (cfg["Site:BaseUrl"] ?? "http://localhost").TrimEnd('/');

    /// <param name="previousPrice">The listing's <see cref="Pricing.Effective"/> price before this change, if it was already live.</param>
    public async Task<int> NotifyAsync(int propertyId, long? previousPrice, CancellationToken ct = default)
    {
        var p = await db.Properties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == propertyId, ct);
        if (p is null || p.Status != ListingStatus.Published) return 0;
        var price = Pricing.Effective(p) ?? 0;
        // A hidden ("توافقی") price is never revealed through a price-drop text.
        var dropped = p.PriceDisplay != PriceDisplay.Negotiable && previousPrice is { } before && price > 0 && price < before;

        var candidates = await db.SavedSearches
            .Where(s => s.Active && s.Deal == p.Deal && (s.NotifyNew || s.NotifyPriceDrop))
            .ToListAsync(ct);
        var already = await db.AlertsSent.Where(a => a.PropertyId == p.Id)
            .Select(a => new { a.SavedSearchId, a.Kind, a.Price }).ToListAsync(ct);

        var sent = 0;
        foreach (var s in candidates.Where(s => AlertMatcher.Matches(s, p)))
        {
            var hadNew = already.Any(a => a.SavedSearchId == s.Id && a.Kind == AlertKind.NewListing);
            AlertKind? kind = !hadNew && s.NotifyNew ? AlertKind.NewListing
                : hadNew && dropped && s.NotifyPriceDrop ? AlertKind.PriceDrop
                : null;
            if (kind is null || already.Any(a => a.SavedSearchId == s.Id && a.Kind == kind && a.Price == price)) continue;

            var result = await sms.SendAsync(s.Phone, Text(kind.Value, p, previousPrice), promotional: true, ct: ct);
            if (!result.Ok) continue;
            db.AlertsSent.Add(new AlertSent { SavedSearchId = s.Id, PropertyId = p.Id, Kind = kind.Value, Price = price });
            sent++;
        }
        await db.SaveChangesAsync(ct);
        return sent;
    }

    private string Text(AlertKind kind, Property p, long? previousPrice)
    {
        var link = $"{_baseUrl}/ملک/{Uri.EscapeDataString(p.Slug)}";
        return kind == AlertKind.NewListing
            ? $"آگهی تازه مطابق شرایط شما:\n{p.Title}، {p.District}\n{Pricing.Label(p)}\n{link}"
            : $"کاهش قیمت! «{p.Title}» از {Fa.Money(previousPrice)} به {Fa.Money(Pricing.Effective(p))}"
              + (p.Deal == DealType.Rent ? " (معادل رهن کامل)" : "") + $" رسید.\n{link}";
    }
}
