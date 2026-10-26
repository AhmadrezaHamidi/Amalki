using System.Globalization;

namespace Amlak.Web.Services;

public static class Fa
{
    public static string Digits(string s) => string.Create(s.Length, s, (span, src) =>
    {
        for (var i = 0; i < src.Length; i++)
            span[i] = src[i] is >= '0' and <= '9' ? (char)('۰' + (src[i] - '0')) : src[i];
    });

    /// <summary>Persian (۰-۹) and Arabic-Indic (٠-٩) digits to ASCII, so typed numbers parse.</summary>
    public static string Latin(string s) => string.Create(s.Length, s, (span, src) =>
    {
        for (var i = 0; i < src.Length; i++)
            span[i] = src[i] switch
            {
                >= '۰' and <= '۹' => (char)('0' + (src[i] - '۰')),
                >= '٠' and <= '٩' => (char)('0' + (src[i] - '٠')),
                _ => src[i],
            };
    });

    /// <summary>Normalizes an Iranian mobile number to 09xxxxxxxxx, or null when it is not one.</summary>
    public static string? Mobile(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var d = new string(Latin(input).Where(char.IsAsciiDigit).ToArray());
        if (d.StartsWith("0098")) d = "0" + d[4..];
        else if (d.StartsWith("98")) d = "0" + d[2..];
        else if (d.StartsWith('9')) d = "0" + d;
        return d.Length == 11 && d.StartsWith("09") ? d : null;
    }

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

    /// <summary>Solar Hijri date in Tehran time (UTC+3:30, no DST since 2022), e.g. "۱۴۰۵/۰۸/۰۴".</summary>
    public static string Date(DateTime utc)
    {
        var d = utc.AddHours(3.5);
        var pc = new PersianCalendar();
        return Digits($"{pc.GetYear(d)}/{pc.GetMonth(d):00}/{pc.GetDayOfMonth(d):00}");
    }
}
