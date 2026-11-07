using Amlak.Domain;
using Amlak.Web.Services;

namespace Amlak.Web.Data;

/// <summary>
/// Realistic sample listings for the demo: Iranian cities and districts with plausible prices per m² (Toman, late 1405),
/// rents derived from the value the way the market does it, and full specs. Deterministic, so every demo looks the same.
/// </summary>
public static class SampleListings
{
    /// <summary>Slugs of generated listings start with this, so a demo database can tell them from real ones.</summary>
    public const string SlugPrefix = "نمونه-";

    // (city, district, sale price per m² in Toman)
    private static readonly (string City, string District, long PerM2)[] Places =
    [
        ("تهران", "الهیه", 310_000_000), ("تهران", "زعفرانیه", 285_000_000), ("تهران", "نیاوران", 240_000_000),
        ("تهران", "فرمانیه", 230_000_000), ("تهران", "قیطریه", 195_000_000), ("تهران", "جردن", 175_000_000),
        ("تهران", "شهرک غرب", 160_000_000), ("تهران", "سعادت‌آباد", 150_000_000), ("تهران", "میرداماد", 150_000_000),
        ("تهران", "ونک", 140_000_000), ("تهران", "پاسداران", 135_000_000), ("تهران", "یوسف‌آباد", 110_000_000),
        ("تهران", "ستارخان", 92_000_000), ("تهران", "پونک", 86_000_000), ("تهران", "صادقیه", 85_000_000),
        ("تهران", "اکباتان", 80_000_000), ("تهران", "جنت‌آباد", 78_000_000), ("تهران", "شهران", 74_000_000),
        ("تهران", "نارمک", 72_000_000), ("تهران", "تهرانپارس", 68_000_000), ("تهران", "پیروزی", 56_000_000),
        ("تهران", "نازی‌آباد", 45_000_000),
        ("کرج", "عظیمیه", 46_000_000), ("کرج", "جهانشهر", 42_000_000), ("کرج", "مهرشهر", 40_000_000),
        ("کرج", "گوهردشت", 38_000_000), ("کرج", "فردیس", 26_000_000),
        ("مشهد", "وکیل‌آباد", 52_000_000), ("مشهد", "سجاد", 48_000_000), ("مشهد", "احمدآباد", 45_000_000), ("مشهد", "هاشمیه", 40_000_000),
        ("اصفهان", "چهارباغ بالا", 66_000_000), ("اصفهان", "جلفا", 60_000_000), ("اصفهان", "مرداویج", 55_000_000), ("اصفهان", "سپاهان‌شهر", 36_000_000),
        ("شیراز", "معالی‌آباد", 56_000_000), ("شیراز", "ارم", 50_000_000), ("شیراز", "قصردشت", 45_000_000), ("شیراز", "زرهی", 40_000_000),
        ("تبریز", "ولیعصر", 41_000_000), ("تبریز", "ائل‌گلی", 38_000_000), ("تبریز", "رشدیه", 30_000_000),
        ("لواسان", "ناران", 120_000_000), ("لواسان", "گلندوک", 85_000_000),
    ];

    // Short-term villas by the Caspian: (city, district, price per night)
    private static readonly (string City, string District, long Night)[] Coast =
    [
        ("رامسر", "سادات‌محله", 6_500_000), ("نوشهر", "خیرود", 5_200_000), ("چالوس", "نمک‌آبرود", 7_800_000),
        ("کلاردشت", "حسن‌کیف", 4_800_000), ("رشت", "گلسار", 3_200_000), ("ماسال", "اولسبلنگاه", 3_900_000),
    ];

    private static readonly string[] ApartmentPhotos = ["07.jpg", "06.jpg", "08.jpg", "09.jpg", "10.jpg", "11.jpg", "12.jpg"];
    private static readonly string[] VillaPhotos = ["01.jpg", "02.jpg", "03.jpg", "04.jpg", "05.jpg", "14.jpg", "15.jpg"];
    private static readonly string[] AptTraits = ["نوساز", "فول امکانات", "نورگیر", "بازسازی‌شده", "کلید نخورده", "دنج و آرام", "ویو ابدی", "شمالی", "لوکس", "تک‌واحدی"];
    private static readonly string[] Extras = ["لابی و نگهبانی ۲۴ ساعته", "استخر و سونا", "روف‌گاردن", "سیستم هوشمند", "کف سرامیک و کابینت هایگلاس", "پکیج و شوفاژ", "اسپلیت در همهٔ اتاق‌ها", "نزدیک مترو", "دسترسی عالی به بزرگراه", "مدرسه و پارک در نزدیکی"];

    /// <param name="count">How many listings to generate.</param>
    public static List<Property> Make(int count, DateTime nowUtc, Action<PropertyPhoto>? onPhoto = null, Func<string, bool>? photoExists = null)
    {
        var rnd = new Random(1405);
        var list = new List<Property>(count);
        for (var i = 0; i < count; i++)
        {
            var p = One(rnd, i, nowUtc, photoExists ?? (_ => false), onPhoto);
            list.Add(p);
        }
        return list;
    }

    private static Property One(Random rnd, int i, DateTime now, Func<string, bool> photoExists, Action<PropertyPhoto>? onPhoto)
    {
        var at = now.AddDays(-rnd.Next(0, 45)).AddHours(-rnd.Next(0, 24));
        var roll = rnd.Next(100);
        var shortTerm = roll < 6;
        var kind = shortTerm ? PropertyKind.Villa : roll < 68 ? PropertyKind.Apartment : roll < 78 ? PropertyKind.Villa
            : roll < 86 ? PropertyKind.Commercial : roll < 94 ? PropertyKind.Office : PropertyKind.Land;

        string city, district;
        long perM2 = 0, night = 0;
        if (shortTerm) (city, district, night) = Coast[rnd.Next(Coast.Length)];
        else
        {
            // Villas and land sit where there is room for them.
            var pool = kind is PropertyKind.Villa or PropertyKind.Land
                ? Places.Where(x => x.City is "لواسان" or "کرج" or "اصفهان" or "شیراز" or "مشهد").ToArray()
                : Places;
            (city, district, perM2) = pool[rnd.Next(pool.Length)];
        }

        var deal = shortTerm ? DealType.ShortTerm
            : kind == PropertyKind.Land ? DealType.Sale
            : rnd.Next(100) < 58 ? DealType.Sale : DealType.Rent;

        var area = kind switch
        {
            PropertyKind.Apartment => Round(rnd.Next(48, 260), 5),
            PropertyKind.Villa => Round(rnd.Next(180, 900), 10),
            PropertyKind.Commercial => Round(rnd.Next(18, 160), 2),
            PropertyKind.Office => Round(rnd.Next(45, 320), 5),
            _ => Round(rnd.Next(300, 3000), 50),
        };
        int? beds = kind switch
        {
            PropertyKind.Apartment => area < 65 ? 1 : area < 110 ? 2 : area < 170 ? 3 : 4,
            PropertyKind.Villa => Math.Clamp(area / 120, 2, 6),
            _ => null,
        };
        var year = kind == PropertyKind.Land ? (int?)null : 1385 + rnd.Next(0, 20);
        int? floor = kind is PropertyKind.Apartment or PropertyKind.Office ? rnd.Next(1, 13) : kind == PropertyKind.Commercial ? 0 : null;
        int? parking = kind == PropertyKind.Land ? null : kind == PropertyKind.Commercial ? rnd.Next(0, 2) : area > 150 ? 2 : 1;

        // Newer, bigger, higher-floor units ask a bit more; commercial ground floors much more; land and villa land less per m².
        var factor = kind switch
        {
            PropertyKind.Commercial => 2.2,
            PropertyKind.Office => 1.1,
            PropertyKind.Villa => 0.75,
            PropertyKind.Land => 0.35,
            _ => 1.0,
        };
        if (year is { } y) factor *= 0.85 + (y - 1385) * 0.012;
        factor *= 0.9 + rnd.NextDouble() * 0.2;
        var value = (long)(area * perM2 * factor);

        Pricing pricing;
        if (deal == DealType.Sale) pricing = Pricing.ForSale(RoundMoney(value, 50_000_000));
        else if (deal == DealType.ShortTerm) pricing = Pricing.ForNightly(RoundMoney(night * (area / 250.0 + 0.4), 100_000));
        else
        {
            // Full rahn is about a quarter of the value; part of it is paid as rent at 3% a month.
            var fullDeposit = value * 0.25;
            var share = new[] { 1.0, 0.5, 0.3, 0.2 }[rnd.Next(4)];
            var deposit = RoundMoney((long)(fullDeposit * share), 10_000_000);
            var rent = share >= 1 ? (long?)null : RoundMoney((long)((fullDeposit - deposit) * 0.03), 500_000);
            pricing = Pricing.ForRent(deposit, rent, convertible: rnd.Next(2) == 0);
        }

        var title = Title(rnd, kind, deal, area, beds, district);
        var desc = Description(rnd, kind, deal, area, beds, year, floor, parking, city, district);
        var details = ListingDetails.Of(title, desc, kind, area, city, district, beds, floor, parking, year) with
        {
            HasElevator = kind is PropertyKind.Apartment or PropertyKind.Office ? (floor > 2 || rnd.Next(3) > 0) : null,
            HasStorage = kind is PropertyKind.Apartment or PropertyKind.Villa ? rnd.Next(4) > 0 : null,
            HasBalcony = kind is PropertyKind.Apartment ? rnd.Next(3) > 0 : null,
            Deed = kind == PropertyKind.Land && rnd.Next(4) == 0 ? DeedType.Manguleh : DeedType.Single,
        };

        var p = Property.Create($"{SlugPrefix}{i + 1}-{Slugger.Make(title)}", details, pricing, at);
        foreach (var f in Photos(rnd, kind).Where(photoExists))
        {
            var photo = p.AddPhoto($"/img/demo/{f}", at);
            onPhoto?.Invoke(photo);
        }
        if (rnd.Next(12) == 0) p.ShowPriceAs(PriceDisplay.Negotiable, at);
        p.Publish(at);
        return p;
    }

    private static string Title(Random rnd, PropertyKind kind, DealType deal, int area, int? beds, string district)
    {
        var m = Fa.Digits(area.ToString());
        return kind switch
        {
            PropertyKind.Apartment => $"آپارتمان {m} متری {Fa.Digits(beds!.Value.ToString())} خوابه {AptTraits[rnd.Next(AptTraits.Length)]} در {district}",
            PropertyKind.Villa when deal == DealType.ShortTerm => $"ویلای {new[] { "جنگلی", "ساحلی", "استخردار", "دوبلکس", "کوهستانی" }[rnd.Next(5)]} {m} متری {district}",
            PropertyKind.Villa => $"{new[] { "ویلای باغ", "خانهٔ ویلایی", "ویلای مدرن", "ویلای دوبلکس" }[rnd.Next(4)]} {m} متری {district}",
            PropertyKind.Commercial => $"مغازهٔ {m} متری {new[] { "بَر خیابان اصلی", "دونبش", "در پاساژ", "با سند تجاری", "دوبله" }[rnd.Next(5)]} {district}",
            PropertyKind.Office => $"دفتر اداری {m} متری {new[] { "موقعیت اداری", "مبله", "با سند اداری", "در برج" }[rnd.Next(4)]} {district}",
            _ => $"زمین {m} متری {new[] { "با جواز ساخت", "دوبر", "با سند تک‌برگ", "مناسب ساخت‌وساز" }[rnd.Next(4)]} {district}",
        };
    }

    private static string Description(Random rnd, PropertyKind kind, DealType deal, int area, int? beds, int? year, int? floor, int? parking,
        string city, string district)
    {
        var parts = new List<string>();
        var what = kind switch
        {
            PropertyKind.Apartment => "آپارتمان", PropertyKind.Villa => "ویلا", PropertyKind.Commercial => "مغازه",
            PropertyKind.Office => "واحد اداری", _ => "زمین",
        };
        parts.Add($"{what} {Fa.Digits(area.ToString())} متری در {district} {city}{(year is { } y ? $"، ساخت {Fa.Digits(y.ToString())}" : "")}.");
        if (beds is { } b) parts.Add($"{Fa.Digits(b.ToString())} اتاق خواب{(floor is { } f && f > 0 ? $"، طبقهٔ {Fa.Digits(f.ToString())}" : "")}{(parking > 0 ? $"، {Fa.Digits(parking.Value.ToString())} پارکینگ" : "")}.");
        var picks = Extras.OrderBy(_ => rnd.Next()).Take(3).ToList();
        if (kind is PropertyKind.Apartment or PropertyKind.Villa) parts.Add("امکانات: " + string.Join("، ", picks) + ".");
        parts.Add(deal switch
        {
            DealType.Sale => "سند آماده و قابل انتقال. بازدید با هماهنگی مشاور.",
            DealType.Rent => "مناسب خانوادهٔ کم‌جمعیت. تخلیه و تحویل فوری.",
            _ => "اجاره روزانه و هفتگی، مناسب خانواده. تحویل کلید حضوری.",
        });
        parts.Add("این آگهی نمونه است و برای نمایش سایت ساخته شده.");
        return string.Join(" ", parts);
    }

    private static IEnumerable<string> Photos(Random rnd, PropertyKind kind)
    {
        string[] pool = kind switch
        {
            PropertyKind.Apartment => ApartmentPhotos,
            PropertyKind.Villa => VillaPhotos,
            PropertyKind.Commercial => ["17.jpg"],
            PropertyKind.Office => ["16.jpg", "13.jpg"],
            _ => ["18.jpg"],
        };
        var start = rnd.Next(pool.Length);
        var n = Math.Min(pool.Length, rnd.Next(1, 4));
        for (var k = 0; k < n; k++) yield return pool[(start + k) % pool.Length];
    }

    private static int Round(int v, int step) => Math.Max(step, v / step * step);

    private static long RoundMoney(long v, long step) => Math.Max(step, (long)Math.Round(v / (double)step) * step);
    private static long RoundMoney(double v, long step) => RoundMoney((long)v, step);
}
