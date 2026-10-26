using Amlak.Web.Domain;
using Amlak.Web.Services;

namespace Amlak.Tests;

/// <summary>Builds domain objects the way the app does: through their factories, so tests obey the same rules.</summary>
internal static class TestData
{
    public static readonly DateTime Now = new(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

    public static Property Listing(string title = "آپارتمان تست", string? slug = null, PropertyKind kind = PropertyKind.Apartment,
        string city = "تهران", string district = "ونک", int area = 90, long price = 5_000_000_000, bool published = true)
    {
        var p = Property.Create(slug ?? Slugger.Make(title), ListingDetails.Of(title, "d", kind, area, city, district), Pricing.ForSale(price), Now);
        if (published) p.Publish(Now);
        p.ClearDomainEvents();
        return p;
    }

    public static Lead Lead(string name = "سارا", string phone = "09121112233", string? message = null) =>
        Amlak.Web.Domain.Lead.Submit(name, PhoneNumber.Parse(phone), message, null, Now);
}
