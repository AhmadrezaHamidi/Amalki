namespace Amlak.Web.Services.Seo;

/// <summary>Short factual sentences that search snippets and AI answer engines can quote as-is.</summary>
public static class SeoText
{
    public static string Location(Property p) =>
        string.Join("، ", new[] { p.District, p.City }.Where(x => !string.IsNullOrWhiteSpace(x)));

    /// <summary>Standalone summary of a listing: what, where, how big, how much, then key specs.</summary>
    public static string Summary(Property p)
    {
        var text = $"{KindPhrase(p.Kind)} {Fa.Digits(p.AreaM2.ToString())} متری در {Location(p)}"
            + p.Deal switch
            {
                DealType.Sale => $" برای فروش به قیمت {Money(p.Price)}",
                DealType.ShortTerm => $" برای اجارهٔ کوتاه‌مدت، هر شب {Money(p.Price)}",
                _ => $" برای رهن و اجاره با رهن {Money(p.Deposit)} و اجارهٔ ماهانهٔ {Money(p.MonthlyRent)}",
            };

        var specs = new List<string>();
        if (p.Bedrooms is { } b) specs.Add($"{Fa.Digits(b.ToString())} خواب");
        if (p.Floor is { } f) specs.Add(f == 0 ? "همکف" : $"طبقهٔ {Fa.Digits(f.ToString())}");
        if (p.Parking is { } pk && pk > 0) specs.Add($"{Fa.Digits(pk.ToString())} پارکینگ");
        if (p.YearBuilt is { } y) specs.Add($"ساخت {Fa.Digits(y.ToString())}");
        return text + (specs.Count > 0 ? "؛ " + string.Join("، ", specs) : "") + ".";
    }

    /// <summary>Opening sentence of a list page, built only from our own data.</summary>
    public static string ListIntro(string siteName, string label, string? city, int total, long? minPrice, long? maxPrice, DealType? deal)
    {
        if (total == 0) return $"در حال حاضر آگهی منتشرشده‌ای برای {label}{In(city)} در {siteName} نیست.";
        var text = $"در حال حاضر {Fa.Digits(total.ToString())} آگهی {label}{In(city)} در {siteName} منتشر شده است";
        if (deal is not null && minPrice is > 0 && maxPrice is > 0)
        {
            var what = deal switch { DealType.Sale => "قیمت‌ها", DealType.ShortTerm => "اجارهٔ هر شب", _ => "اجارهٔ ماهانه" };
            text += minPrice == maxPrice
                ? $"؛ {what} {Money(minPrice)} است"
                : $"؛ {what} از {Money(minPrice)} تا {Money(maxPrice)} است";
        }
        return text + ".";
    }

    // "تجاری"/"اداری" are adjectives; in a sentence they need a noun.
    private static string KindPhrase(PropertyKind k) =>
        k is PropertyKind.Commercial or PropertyKind.Office ? "ملک " + Labels.Kind(k) : Labels.Kind(k);

    private static string In(string? city) => string.IsNullOrWhiteSpace(city) ? "" : " در " + city.Trim();

    private static string Money(long? toman) => toman is null or 0 ? "توافقی" : Fa.Money(toman) + " تومان";
}
