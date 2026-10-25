namespace Amlak.Web.Services.Publishing;

public static class CaptionBuilder
{
    public static string Build(Property p, string baseUrl)
    {
        var deal = p.Deal == DealType.Sale ? "فروش" : "اجاره";
        var lines = new List<string>
        {
            $"🏠 {p.Title}",
            $"📍 {p.City}، {p.District}",
            "📐 " + Fa.Digits(p.AreaM2.ToString()) + " متر" + (p.Bedrooms is { } b ? $" | {Fa.Digits(b.ToString())} خواب" : ""),
        };

        if (p.Deal == DealType.Sale)
            lines.Add($"💰 قیمت: {Fa.Money(p.Price)} تومان");
        else
        {
            lines.Add($"💰 رهن: {Fa.Money(p.Deposit)} تومان");
            lines.Add($"🗓 اجارهٔ ماهانه: {Fa.Money(p.MonthlyRent)} تومان");
        }

        lines.Add($"🔗 {baseUrl.TrimEnd('/')}/ملک/{Uri.EscapeDataString(p.Slug)}");
        lines.Add($"#{deal} #{Tag(p.City)} #{Tag(p.District)}");
        return string.Join("\n", lines);
    }

    // Hashtags cannot contain spaces or punctuation.
    private static string Tag(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
}
