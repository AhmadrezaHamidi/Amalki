namespace Amlak.Web.Services;

/// <summary>One price plan on /تعرفه‌ها. Price is in Toman; 0 is free.</summary>
public sealed record Plan(string Id, string Name, string Audience, long Price, string Per, string Pitch, string[] Features, bool Highlight = false);

/// <summary>
/// Price plans for owners and for agencies. Defaults live here; the "Plans:{id}:Price" setting overrides
/// a price without a new release. There is no online payment yet, so every plan is ordered through a consultant.
/// </summary>
public static class Plans
{
    public const string Owners = "owners", Agencies = "agencies";

    private static readonly Plan[] Defaults =
    [
        new("free", "رایگان", Owners, 0, "", "برای فروش یا اجارهٔ یک ملک بدون هزینه.",
            ["۱ آگهی فعال", "۳۰ روز نمایش در سایت", "تا ۵ عکس", "بررسی آگهی پیش از انتشار"]),
        new("featured", "ویژه", Owners, 190_000, "هر آگهی", "آگهی‌تان بالای فهرست و در کانال‌ها دیده می‌شود.",
            ["همهٔ امکانات رایگان", "۷ روز بالای فهرست با برچسب «ویژه»", "انتشار در کانال تلگرام و بله", "تا ۱۵ عکس", "گزارش بازدید آگهی"], Highlight: true),
        new("royal", "رویال", Owners, 490_000, "هر آگهی", "کار را به ما بسپارید؛ از عکاسی تا بازدید.",
            ["همهٔ امکانات ویژه", "۳۰ روز ویژه", "عکاسی حرفه‌ای از ملک", "مشاور اختصاصی و هماهنگی بازدیدها", "قیمت‌گذاری کارشناسی"]),
        new("agency-basic", "آژانس پایه", Agencies, 1_200_000, "ماهانه", "برای مشاوران مستقل و دفترهای کوچک.",
            ["۲۰ آگهی فعال هم‌زمان", "۵ نردبان در ماه", "نام و تلفن دفتر روی آگهی‌ها", "پنل مدیریت آگهی"]),
        new("agency-pro", "آژانس حرفه‌ای", Agencies, 3_500_000, "ماهانه", "برای دفترهای املاک با چند مشاور.",
            ["۱۰۰ آگهی فعال هم‌زمان", "۲۰ نردبان در ماه", "صفحهٔ اختصاصی دفتر و مشاوران", "درخواست‌های تماس مستقیم به مشاوران", "پیامک به مشتریان منتظر"], Highlight: true),
    ];

    public static IReadOnlyList<Plan> All(IConfiguration cfg) =>
        Defaults.Select(p => long.TryParse(cfg[$"Plans:{p.Id}:Price"], out var price) && price >= 0 ? p with { Price = price } : p).ToList();

    public static Plan? Find(IConfiguration cfg, string? id) => All(cfg).FirstOrDefault(p => p.Id == id);
}
