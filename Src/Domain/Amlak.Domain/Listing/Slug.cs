using System.Text.RegularExpressions;
using Amlak.Domain.Listing.Exceptions;

namespace Amlak.Domain.Listing;

/// <summary>
/// ساخت نشانی صفحه (slug) از عنوان آگهی.
/// حروف و ارقام فارسی حفظ می‌شوند (گوگل URL فارسی را ایندکس می‌کند)، ی/ک عربی یکسان می‌شوند.
/// </summary>
public static partial class Slug
{
    public const int MaxLength = 120;

    public static string From(string title)
    {
        var s = title.Trim().ToLowerInvariant().Replace('ي', 'ی').Replace('ك', 'ک');
        s = Disallowed().Replace(s, "-").Trim('-');
        if (s.Length > MaxLength) s = s[..MaxLength].TrimEnd('-');
        if (s.Length == 0) throw new InvalidSlugException();
        return s;
    }

    /// <summary>نسخهٔ یکتا: «عنوان»، «عنوان-2»، «عنوان-3» …</summary>
    public static string WithSuffix(string slug, int n) => n <= 1 ? slug : $"{slug}-{n}";

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex Disallowed();
}
