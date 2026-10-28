using Amlak.Web.Data;
using Amlak.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Services.Seo;

public static class SeoEndpoints
{
    /// <summary>
    /// Public base URL comes from configuration, not the Host header, so a forged Host
    /// cannot poison the sitemap. Falls back to the request only when unset.
    /// </summary>
    public static string BaseUrl(IConfiguration cfg, HttpRequest req) =>
        (cfg["Site:BaseUrl"] ?? $"{req.Scheme}://{req.Host}").TrimEnd('/');

    public static void MapSeo(this WebApplication app)
    {
        app.MapGet("/robots.txt", (IConfiguration cfg, HttpRequest req) =>
            Results.Text(SeoFiles.Robots(BaseUrl(cfg, req)), "text/plain; charset=utf-8"))
            .CacheOutput(SiteCache.Listings);

        app.MapGet("/sitemap.xml", async (AppDbContext db, IConfiguration cfg, HttpRequest req, CancellationToken ct) =>
        {
            var items = await db.Properties.AsNoTracking().Include(p => p.Photos)
                .Where(p => p.Status == ListingStatus.Published)
                .OrderByDescending(p => p.PublishedAtUtc).Take(SeoFiles.MaxSitemapUrls).ToListAsync(ct);
            return Results.Text(SeoFiles.Sitemap(items, BaseUrl(cfg, req)), "application/xml; charset=utf-8");
        }).CacheOutput(SiteCache.Listings);

        app.MapGet("/llms.txt", async (AppDbContext db, IConfiguration cfg, HttpRequest req, CancellationToken ct) =>
        {
            var items = await db.Properties.AsNoTracking()
                .Where(p => p.Status == ListingStatus.Published)
                .OrderByDescending(p => p.PublishedAtUtc).Take(20).ToListAsync(ct);
            return Results.Text(SeoFiles.Llms(cfg["Site:Name"] ?? "آشیانه", BaseUrl(cfg, req), items), "text/plain; charset=utf-8");
        }).CacheOutput(SiteCache.Listings);

        // Short link for SMS: a Persian slug is long once encoded, and every 67 characters is another billed part.
        app.MapGet("/p/{id:int}", async (int id, AppDbContext db, IConfiguration cfg, HttpRequest req, CancellationToken ct) =>
        {
            var p = await db.Properties.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.Status == ListingStatus.Published, ct);
            return p is null ? Results.NotFound() : Results.Redirect(SeoFiles.PropertyUrl(BaseUrl(cfg, req), p), permanent: true);
        });

        // Cached pages cannot carry a per-visitor antiforgery token, so their forms fetch one here just before submitting.
        app.MapGet("/antiforgery-token", (HttpContext ctx, Microsoft.AspNetCore.Antiforgery.IAntiforgery af) =>
        {
            var tokens = af.GetAndStoreTokens(ctx);
            ctx.Response.Headers.CacheControl = "no-store";
            return Results.Json(new { field = tokens.FormFieldName, token = tokens.RequestToken });
        });
    }
}
