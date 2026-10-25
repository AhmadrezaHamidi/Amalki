using Amlak.Application.Common;
using Amlak.Domain.Listing.Aggregates;
using Amlak.Domain.Listing.Enums;

namespace Amlak.Application.Publishing;

/// <summary>متن پست کانال: عنوان، محل، متراژ، قیمت، لینک صفحهٔ آگهی و هشتگ</summary>
public static class CaptionBuilder
{
    public static string Build(Property p, string siteBaseUrl)
    {
        var lines = new List<string>
        {
            $"🏠 {p.Title}",
            $"📍 {p.City}، {p.District}",
            "📐 " + Fa.Digits(p.AreaM2.ToString()) + " متر" + (p.Bedrooms is { } b ? $" | {Fa.Digits(b.ToString())} خواب" : ""),
        };

        if (p.Deal == DealType.Sale)
        {
            lines.Add($"💰 قیمت: {Fa.Money(p.Price)} تومان");
        }
        else
        {
            lines.Add($"💰 رهن: {Fa.Money(p.Deposit)} تومان");
            lines.Add($"🗓 اجارهٔ ماهانه: {Fa.Money(p.MonthlyRent)} تومان");
        }

        lines.Add($"🔗 {siteBaseUrl.TrimEnd('/')}/ملک/{Uri.EscapeDataString(p.Slug)}");
        lines.Add($"#{(p.Deal == DealType.Sale ? "فروش" : "اجاره")} #{Tag(p.City)} #{Tag(p.District)}");
        return string.Join("\n", lines);
    }

    // هشتگ نمی‌تواند فاصله یا علامت داشته باشد
    private static string Tag(string s) => new string(s.Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
}
