using System.Globalization;

namespace Amlak.Web.Services;

public static class Fa
{
    public static string Digits(string s) => string.Create(s.Length, s, (span, src) =>
    {
        for (var i = 0; i < src.Length; i++)
            span[i] = src[i] is >= '0' and <= '9' ? (char)('۰' + (src[i] - '0')) : src[i];
    });

    /// <summary>Formats Toman amounts as "۱۲٫۵ میلیارد" / "۴۰۰ میلیون".</summary>
    public static string Money(long? toman)
    {
        if (toman is null or 0) return "توافقی";
        var v = toman.Value;
        string t = v >= 1_000_000_000 ? $"{v / 1_000_000_000m:0.##} میلیارد"
                 : v >= 1_000_000 ? $"{v / 1_000_000m:0.##} میلیون"
                 : $"{v:N0}";
        return Digits(t.Replace('.', '٫'));
    }

    public static string Kind(PropertyKind k) => k switch
    {
        PropertyKind.Apartment => "آپارتمان",
        PropertyKind.Villa => "ویلا",
        PropertyKind.Land => "زمین",
        PropertyKind.Commercial => "ملک تجاری",
        PropertyKind.Office => "ملک اداری",
        _ => "ملک",
    };

    /// <summary>Solar Hijri date in Tehran time (UTC+3:30, no DST since 2022), e.g. "۱۴۰۵/۰۸/۰۴".</summary>
    public static string Date(DateTime utc)
    {
        var d = utc.AddHours(3.5);
        var pc = new PersianCalendar();
        return Digits($"{pc.GetYear(d)}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00}");
    }
}
