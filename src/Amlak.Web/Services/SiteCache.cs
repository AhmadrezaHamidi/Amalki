using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Amlak.Web.Services;

/// <summary>
/// Server-side output cache for public listing pages (home, list, detail, sitemap, llms.txt).
/// Entries are tagged and dropped as soon as a listing or photo is saved, so the expiry is only a safety net.
/// Signed-in requests and responses that set cookies are never cached (default policy).
/// </summary>
public static class SiteCache
{
    public const string Listings = "listings";

    public static void AddSiteCache(this IServiceCollection services)
    {
        services.AddOutputCache(o => o.AddPolicy(Listings, b => b.Expire(TimeSpan.FromMinutes(10)).Tag(Listings)
            // One copy per UI variant while visitors can preview them (UiVariants).
            .VaryByValue(ctx => new KeyValuePair<string, string>("ui", UiVariants.Current(ctx)?.Id ?? ""))));
        services.AddSingleton<ListingCacheInterceptor>();
    }
}

/// <summary>
/// Evicts the listing pages after any save that adds, changes or removes a listing or photo,
/// so every write path (admin pages, future imports) keeps the cache honest without remembering to.
/// </summary>
public sealed class ListingCacheInterceptor(IOutputCacheStore store) : SaveChangesInterceptor
{
    private static readonly ConditionalWeakTable<DbContext, object> Dirty = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> result, CancellationToken ct = default)
    {
        Mark(e.Context);
        return base.SavingChangesAsync(e, result, ct);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> result)
    {
        Mark(e.Context);
        return base.SavingChanges(e, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int result, CancellationToken ct = default)
    {
        if (e.Context is { } c && Dirty.Remove(c)) await store.EvictByTagAsync(SiteCache.Listings, ct);
        return result;
    }

    public override int SavedChanges(SaveChangesCompletedEventData e, int result)
    {
        if (e.Context is { } c && Dirty.Remove(c)) store.EvictByTagAsync(SiteCache.Listings, default).AsTask().GetAwaiter().GetResult();
        return result;
    }

    private static void Mark(DbContext? c)
    {
        if (c is null) return;
        var touched = c.ChangeTracker.Entries().Any(x =>
            (x.State is EntityState.Added or EntityState.Modified or EntityState.Deleted) && x.Entity is Property or PropertyPhoto);
        if (touched) Dirty.AddOrUpdate(c, true);
    }
}
