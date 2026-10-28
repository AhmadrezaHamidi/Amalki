namespace Amlak.Web.Services;

/// <summary>
/// The ten UI variants of the public site (wwwroot/css/variants.css, docs/UI-VARIANTS.md), each modelled on a
/// leading Iranian property site. The site-wide default is <c>Ui:Variant</c>; while <c>Ui:Preview</c> is on, a visitor
/// can try another one with <c>?ui=id</c> (remembered in a cookie) or the floating switcher.
/// </summary>
public static class UiVariants
{
    public sealed record Variant(string Id, string Name, string InspiredBy, string Summary, string[] Swatch);

    public const string Cookie = "ui";
    private const string ItemKey = "ui-variant";

    public static readonly IReadOnlyList<Variant> All =
    [
        new("divar", "فید ساده", "دیوار", "قرمز تیره، بدون کادر و سایه، فهرست فشرده با خط جداکننده؛ سریع‌ترین مرور روی موبایل.", ["#A62626", "#FFFFFF", "#E6E6E6"]),
        new("sheypoor", "بازار دوستانه", "شیپور", "آبی فیروزه‌ای، کارت‌های گرد سفید روی زمینهٔ خاکستری روشن و چیپ‌های فیلتر برجسته.", ["#0A7EA4", "#F4F6F8", "#E3F3F9"]),
        new("kilid", "پورتال تخصصی", "کیلید", "هدر آبی با جست‌وجوی بزرگ، شبکهٔ کارت عکس‌محور و دکمهٔ نارنجی.", ["#1958D4", "#F26B1D", "#E8F0FD"]),
        new("melkradar", "داده و قیمت", "ملک‌رادار", "سرمه‌ای و کهربایی، قیمت درشت بالای کارت و مشخصات به شکل برچسب؛ برای مقایسهٔ عددی.", ["#0F2A4A", "#F59E0B", "#E7EDF5"]),
        new("ihome", "آرام و جادار", "آی‌هوم", "بنفش، گوشه‌های خیلی گرد، فضای سفید زیاد و کارت‌هایی که با هاور بالا می‌آیند.", ["#6D28D9", "#F1EAFE", "#FFFFFF"]),
        new("jabama", "سفر و اقامت", "جاباما", "هدر نارنجی گرم، جست‌وجوی کپسولی و قیمت پررنگ؛ مناسب اجارهٔ روزانه و ویلا.", ["#E8590C", "#FF7A1A", "#FFF0E5"]),
        new("jajiga", "ویلا و طبیعت", "جاجیگا", "سبز جنگلی روی کرم گرم؛ حس آرام و طبیعی.", ["#1F7A4C", "#FAF7F0", "#E5F3EB"]),
        new("shab", "صمیمی و نرم", "شب / اتاقک", "مرجانی، دکمه‌ها و تب‌های کپسولی، زمینهٔ صورتی خیلی روشن.", ["#E11D48", "#FFFBFC", "#FFE8EE"]),
        new("delta", "لوکس", "دلتا", "همیشه تیره با طلایی، گوشه‌های تیز؛ برای املاک لوکس و آژانس‌های بالارده.", ["#0B1220", "#C9A24B", "#18233A"]),
        new("melkana", "میز کار مشاور", "ملکانه / فایل‌های آژانس", "نیلی روی خاکستری، کارت‌های جمع‌وجور با نوار رنگی نوع معامله؛ شبیه فهرست فایل مشاور.", ["#4338CA", "#F1F5F9", "#0D9488"]),
    ];

    public static Variant? Find(string? id) => All.FirstOrDefault(v => string.Equals(v.Id, id, StringComparison.OrdinalIgnoreCase));

    public static bool Preview(IConfiguration cfg) => cfg.GetValue<bool>("Ui:Preview");

    /// <summary>The variant for this request, or null for the original v4 look.</summary>
    public static Variant? Current(HttpContext ctx) => ctx.Items[ItemKey] as Variant;

    /// <summary>Resolves the variant once per request and keeps a <c>?ui=</c> choice in a cookie.</summary>
    public static IApplicationBuilder UseUiVariants(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var cfg = ctx.RequestServices.GetRequiredService<IConfiguration>();
        var chosen = Find(cfg["Ui:Variant"]);
        if (Preview(cfg))
        {
            var asked = ctx.Request.Query[Cookie].ToString();
            if (asked.Length > 0)
            {
                // Any unknown value (e.g. ?ui=default) goes back to the site default.
                if (Find(asked) is not { } picked) ctx.Response.Cookies.Delete(Cookie);
                else
                {
                    chosen = picked;
                    ctx.Response.Cookies.Append(Cookie, picked.Id, new CookieOptions
                    {
                        MaxAge = TimeSpan.FromDays(30), HttpOnly = true, SameSite = SameSiteMode.Lax, IsEssential = true,
                    });
                }
            }
            else if (Find(ctx.Request.Cookies[Cookie]) is { } fromCookie) chosen = fromCookie;
        }
        ctx.Items[ItemKey] = chosen;
        await next();
    });
}
