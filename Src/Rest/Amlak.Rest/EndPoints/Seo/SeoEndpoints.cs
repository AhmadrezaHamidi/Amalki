using Amlak.Rest.EndPoints.Seo;
using Microsoft.Extensions.Configuration;

namespace Amlak.Rest.EndPoints;

/// <summary>
/// sitemap.xml، robots.txt و llms.txt در ریشهٔ دامنه. فرانت React این‌ها را نمی‌تواند بسازد،
/// پس وب‌سرور جلویی این سه مسیر را به API می‌فرستد (docs/FRONTEND.md).
/// </summary>
public class SeoEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/robots.txt", Robots).AllowAnonymous().ExcludeFromDescription();
        app.MapGet("/sitemap.xml", Sitemap).AllowAnonymous().ExcludeFromDescription();
        app.MapGet("/llms.txt", Llms).AllowAnonymous().ExcludeFromDescription();
    }

    /// <summary>
    /// آدرس پایه از تنظیمات می‌آید نه هدر Host، تا Host جعلی نقشهٔ سایت را آلوده نکند.
    /// </summary>
    private static string BaseUrl(IConfiguration cfg, HttpRequest req) =>
        (cfg["Site:BaseUrl"] ?? $"{req.Scheme}://{req.Host}").TrimEnd('/');

    private static IResult Robots(IConfiguration cfg, HttpRequest req)
        => Results.Text(SeoFiles.Robots(BaseUrl(cfg, req)), "text/plain; charset=utf-8");

    private static async Task<IResult> Sitemap(IQueryBus bus, IConfiguration cfg, HttpRequest req, CancellationToken ct)
    {
        var entries = await bus.DispatchAsync<IReadOnlyList<SitemapEntry>>(new GetSitemapEntriesQuery(), ct);
        return Results.Text(SeoFiles.Sitemap(entries, BaseUrl(cfg, req)), "application/xml; charset=utf-8");
    }

    private static async Task<IResult> Llms(IQueryBus bus, IConfiguration cfg, HttpRequest req, CancellationToken ct)
    {
        var latest = await bus.DispatchAsync<PagedResult<PropertyCard>>(new SearchPropertiesQuery(PageSize: 20), ct);
        return Results.Text(SeoFiles.Llms(cfg["Site:Name"] ?? "آشیانه", BaseUrl(cfg, req), latest.Items), "text/plain; charset=utf-8");
    }
}
