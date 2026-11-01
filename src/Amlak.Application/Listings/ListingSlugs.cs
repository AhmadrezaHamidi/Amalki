using Amlak.Application.Abstractions;

namespace Amlak.Application.Listings;

public static class ListingSlugs
{
    /// <summary>A slug for a new listing that no other listing uses yet ("title", "title-2", ...).</summary>
    public static async Task<string> UniqueAsync(IListingRepository listings, string title, CancellationToken ct = default)
    {
        var baseSlug = Slugger.Make(title);
        if (baseSlug.Length == 0) baseSlug = Slugger.Fallback;
        var slug = baseSlug; var n = 2;
        while (await listings.SlugExistsAsync(slug, ct)) slug = $"{baseSlug}-{n++}";
        return slug;
    }
}
