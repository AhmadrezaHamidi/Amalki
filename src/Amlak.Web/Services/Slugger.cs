using System.Text.RegularExpressions;
using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

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

    /// <summary>A slug for a new listing that no other listing uses yet ("title", "title-2", ...).</summary>
    public static async Task<string> UniqueAsync(AppDbContext db, string title)
    {
        var baseSlug = Make(title);
        if (baseSlug.Length == 0) baseSlug = "ملک";
        var slug = baseSlug; var n = 2;
        while (await db.Properties.AnyAsync(x => x.Slug == slug)) slug = $"{baseSlug}-{n++}";
        return slug;
    }

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex Disallowed();
}
