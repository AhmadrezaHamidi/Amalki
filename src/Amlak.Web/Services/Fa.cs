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
}
