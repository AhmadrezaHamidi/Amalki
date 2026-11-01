using System.Text.RegularExpressions;

namespace Amlak.Domain;

/// <summary>URL slugs for listings and places. Uniqueness among listings is checked by the use case (ListingSlugs).</summary>
public static partial class Slugger
{
    /// <summary>The slug used when a title has no letters or digits at all.</summary>
    public const string Fallback = "ملک";

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
