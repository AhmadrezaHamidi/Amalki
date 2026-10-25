using Amlak.Web.Data;
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
            Results.Text(SeoFiles.Robots(BaseUrl(cfg, req)), "text/plain; charset=utf-8"));

        app.MapGet("/sitemap.xml", async (AppDbContext db, IConfiguration cfg, HttpRequest req, CancellationToken ct) =>
        {
            var items = await db.Properties.AsNoTracking().Include(p => p.Photos)
                .Where(p => p.Status == ListingStatus.Published)
                .OrderByDescending(p => p.PublishedAtUtc).Take(SeoFiles.MaxSitemapUrls).ToListAsync(ct);
            return Results.Text(SeoFiles.Sitemap(items, BaseUrl(cfg, req)), "application/xml; charset=utf-8");
        });

        app.MapGet("/llms.txt", async (AppDbContext db, IConfiguration cfg, HttpRequest req, CancellationToken ct) =>
        {
            var items = await db.Properties.AsNoTracking()
                .Where(p => p.Status == ListingStatus.Published)
                .OrderByDescending(p => p.PublishedAtUtc).Take(20).ToListAsync(ct);
            return Results.Text(SeoFiles.Llms(cfg["Site:Name"] ?? "آشیانه", BaseUrl(cfg, req), items), "text/plain; charset=utf-8");
        });
    }
}
