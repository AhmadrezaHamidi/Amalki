using Amlak.Web.Domain;
using Amlak.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Data;

/// <summary>Development-only sample listings so the public pages have something to render.</summary>
public static class DevSeed
{
    /// <param name="webRoot">wwwroot; demo photos are taken from img/demo when tools/fetch-demo-photos.sh has fetched them.</param>
    public static async Task RunAsync(AppDbContext db, string? webRoot = null)
    {
        await db.Database.MigrateAsync();
        if (await db.Properties.AnyAsync()) return;

        var now = DateTime.UtcNow;
        Property Make(string title, DealType deal, PropertyKind kind, string city, string district, int area, int? beds,
            long? price, long? deposit, long? rent, int ageDays, params string[] photos)
        {
            var at = now.AddDays(-ageDays);
            var p = Property.Create(Slugger.Make(title),
                ListingDetails.Of(title, $"{title} در {district} {city}. نمونهٔ آزمایشی برای نمایش طراحی.", kind, area, city, district, beds),
                Pricing.For(deal, price, deposit, rent), at);
            foreach (var f in photos)
                if (webRoot is not null && File.Exists(Path.Combine(webRoot, "img", "demo", f))) p.AddPhoto($"/img/demo/{f}", at);
            p.Publish(at);
            return p;
        }

        db.Properties.AddRange(
            Make("آپارتمان ۱۲۰ متری نوساز", DealType.Sale, PropertyKind.Apartment, "تهران", "سعادت‌آباد", 120, 3, 12_500_000_000, null, null, 1, "07.jpg", "10.jpg", "11.jpg"),
            Make("سوئیت مبله نزدیک مترو", DealType.Rent, PropertyKind.Apartment, "تهران", "ونک", 60, 1, null, 400_000_000, 8_000_000, 2, "06.jpg", "11.jpg"),
            Make("ویلای باغ ۵۰۰ متری", DealType.Sale, PropertyKind.Villa, "کرج", "مشکین‌دشت", 500, 4, 28_000_000_000, null, null, 3, "03.jpg", "05.jpg"),
            Make("مغازهٔ دوبله خیابان اصلی", DealType.Rent, PropertyKind.Commercial, "شیراز", "معالی‌آباد", 80, null, null, 600_000_000, 25_000_000, 4, "17.jpg"),
            Make("آپارتمان ۸۵ متری فول امکانات", DealType.Sale, PropertyKind.Apartment, "تهران", "پونک", 85, 2, 7_200_000_000, null, null, 1, "09.jpg", "10.jpg"),
            Make("خانهٔ ویلایی دوبلکس", DealType.Sale, PropertyKind.Villa, "اصفهان", "جلفا", 240, 4, 18_000_000_000, null, null, 2, "04.jpg", "08.jpg"),
            Make("ویلای استخردار شمال", DealType.ShortTerm, PropertyKind.Villa, "رامسر", "سادات‌محله", 300, 3, 6_500_000, null, null, 2, "02.jpg", "14.jpg"),
            Make("آپارتمان ۱۰۰ متری رهن کامل", DealType.Rent, PropertyKind.Apartment, "مشهد", "وکیل‌آباد", 100, 2, null, 2_500_000_000, null, 3, "12.jpg", "07.jpg"),
            Make("دفتر اداری ۱۵۰ متری", DealType.Rent, PropertyKind.Office, "تهران", "جردن", 150, null, null, 1_000_000_000, 60_000_000, 5, "16.jpg", "13.jpg"),
            Make("زمین ۱۰۰۰ متری با سند تک‌برگ", DealType.Sale, PropertyKind.Land, "کرج", "محمدشهر", 1000, null, 9_000_000_000, null, null, 6, "18.jpg"),
            Make("ویلای مدرن با نمای شیشه‌ای", DealType.Sale, PropertyKind.Villa, "لواسان", "ناران", 650, 5, 95_000_000_000, null, null, 4, "01.jpg", "15.jpg"),
            Make("آپارتمان ۷۰ متری نزدیک دانشگاه", DealType.Rent, PropertyKind.Apartment, "تبریز", "ولیعصر", 70, 2, null, 300_000_000, 12_000_000, 1, "08.jpg", "06.jpg"));
        // Sample data must not be posted to the real channels.
        foreach (var p in db.ChangeTracker.Entries<Property>()) p.Entity.ClearDomainEvents();
        await db.SaveChangesAsync();
    }
}
