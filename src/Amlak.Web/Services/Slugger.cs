using System.Text.RegularExpressions;

namespace Amlak.Web.Services;

public static partial class Slugger
{
    // Keeps Persian letters and digits; Google indexes Persian URLs fine.
    public static string Make(string text)
    {
        var s = PersianText.Normalize(text).ToLowerInvariant();
        s = Disallowed().Replace(s, "-");
        return s.Trim('-');
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex Disallowed();
}
