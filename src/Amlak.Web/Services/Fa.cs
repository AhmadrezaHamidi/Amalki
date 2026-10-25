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
}
