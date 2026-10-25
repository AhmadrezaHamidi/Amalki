using System.Text.RegularExpressions;

namespace Amlak.Web.Services;

public static partial class Slugger
{
    // Keeps Persian letters and digits; Google indexes Persian URLs fine.
    public static string Make(string text)
    {
        var s = text.Trim().ToLowerInvariant().Replace('ي', 'ی').Replace('ك', 'ک');
        s = Disallowed().Replace(s, "-");
        return s.Trim('-');
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex Disallowed();
}
