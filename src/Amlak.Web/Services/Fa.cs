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

    /// <summary>Parses a whole number typed with Persian, Arabic or Latin digits and any thousands separators.</summary>
    public static long? ParseLong(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var digits = new string(s.Select(c => c switch
        {
            >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
            >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
            _ => c,
        }).Where(c => c is not (',' or '٬' or '،' or ' ' or '\u200c')).ToArray());
        return long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var v) ? v : null;
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
        var pc = new PersianCalendar();
        return Digits($"{pc.GetYear(local):0000}/{pc.GetMonth(local):00}/{pc.GetDayOfMonth(local):00} {local:HH:mm}");
    }
}
