namespace Amlak.Web.Services;

public static class Fa
{
    public static string Digits(string s) => string.Create(s.Length, s, (span, src) =>
    {
        for (var i = 0; i < src.Length; i++)
            span[i] = src[i] is >= '0' and <= '9' ? (char)('۰' + (src[i] - '0')) : src[i];
    });

    /// <summary>Persian (۰-۹) and Arabic-Indic (٠-٩) digits to ASCII, so typed numbers parse.</summary>
    public static string Latin(string s) => PersianText.LatinDigits(s);

    /// <summary>Normalizes an Iranian mobile number to 09xxxxxxxxx, or null when it is not one.</summary>
    public static string? Mobile(string? input) => PhoneNumber.TryParse(input, out var p) ? p.Value : null;

    /// <summary>A whole amount typed with any digits and separators ("۱٬۵۰۰٬۰۰۰"), or null when there is none.</summary>
    public static long? Amount(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var digits = new string(Latin(s).Where(char.IsAsciiDigit).ToArray());
        return digits.Length is > 0 and < 19 ? long.Parse(digits) : null;
    }

    /// <summary>Iran Standard Time (UTC+3:30, no daylight saving since 1401).</summary>
    public static TimeZoneInfo Tehran { get; } = FindTehran();

    private static TimeZoneInfo FindTehran()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone("Tehran", TimeSpan.FromMinutes(210), "Tehran", "Tehran");
        }
    }

    /// <summary>"۱۴۰۵/۰۸/۰۴ ۱۰:۰۰" in Tehran time.</summary>
    public static string When(DateTime utc)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Tehran);
        var pc = new System.Globalization.PersianCalendar();
        return Digits($"{pc.GetYear(local):0000}/{pc.GetMonth(local):00}/{pc.GetDayOfMonth(local):00} {local:HH:mm}");
    }

    /// <summary>Formats Toman amounts as "۱۲٫۵ میلیارد" / "۴۰۰ میلیون".</summary>
    public static string Money(long? toman)
    {
        if (toman is null or 0) return "توافقی";
        var v = toman.Value;
        // Invariant culture: the server's culture must not change the separators (e.g. "12,5" on a German host).
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        string t = v >= 1_000_000_000 ? string.Create(inv, $"{v / 1_000_000_000m:0.##} میلیارد")
                 : v >= 1_000_000 ? string.Create(inv, $"{v / 1_000_000m:0.##} میلیون")
                 : string.Create(inv, $"{v:N0}");
        return Digits(t.Replace('.', '٫'));
    }

    /// <summary>Relative time the way listing sites show it: "لحظاتی پیش"، "۳ ساعت پیش"، "۲ هفته پیش".</summary>
    public static string Ago(DateTime utc, DateTime? nowUtc = null)
    {
        var d = (nowUtc ?? DateTime.UtcNow) - utc;
        string t = d.TotalMinutes < 1 ? "لحظاتی"
                 : d.TotalHours < 1 ? $"{(int)d.TotalMinutes} دقیقه"
                 : d.TotalDays < 1 ? $"{(int)d.TotalHours} ساعت"
                 : d.TotalDays < 7 ? ((int)d.TotalDays == 1 ? "دیروز" : $"{(int)d.TotalDays} روز")
                 : d.TotalDays < 30 ? $"{(int)(d.TotalDays / 7)} هفته"
                 : d.TotalDays < 365 ? $"{(int)(d.TotalDays / 30)} ماه"
                 : $"{(int)(d.TotalDays / 365)} سال";
        return t == "دیروز" ? t : Digits(t) + " پیش";
    }
}
