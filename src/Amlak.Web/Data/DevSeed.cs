using Amlak.Web.Domain;
using Amlak.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Data;

/// <summary>Development-only sample listings so the public pages have something to render.</summary>
public static class DevSeed
{
    public static async Task RunAsync(AppDbContext db)
    {
        await db.Database.MigrateAsync();
        if (await db.Properties.AnyAsync()) return;

        var now = DateTime.UtcNow;
        Property Make(string title, DealType deal, PropertyKind kind, string city, string district, int area, int? beds,
            long? price, long? deposit, long? rent, int ageDays)
        {
            var at = now.AddDays(-ageDays);
            var p = Property.Create(Slugger.Make(title),
                ListingDetails.Of(title, $"{title} در {district} {city}. نمونهٔ آزمایشی برای نمایش طراحی.", kind, area, city, district, beds),
                Pricing.For(deal, price, deposit, rent), at);
            p.Publish(at);
            return p;
        }

        db.Properties.AddRange(
            Make("آپارتمان ۱۲۰ متری نوساز", DealType.Sale, PropertyKind.Apartment, "تهران", "سعادت‌آباد", 120, 3, 12_500_000_000, null, null, 1),
            Make("سوئیت مبله نزدیک مترو", DealType.Rent, PropertyKind.Apartment, "تهران", "ونک", 60, 1, null, 400_000_000, 8_000_000, 2),
            Make("ویلای باغ ۵۰۰ متری", DealType.Sale, PropertyKind.Villa, "کرج", "مشکین‌دشت", 500, 4, 28_000_000_000, null, null, 3),
            Make("مغازهٔ دوبله خیابان اصلی", DealType.Rent, PropertyKind.Commercial, "شیراز", "معالی‌آباد", 80, null, null, 600_000_000, 25_000_000, 4));
        // Sample data must not be posted to the real channels.
        foreach (var p in db.ChangeTracker.Entries<Property>()) p.Entity.ClearDomainEvents();
        await db.SaveChangesAsync();
    }
}
