using Amlak.Rest.EndPoints.Listing;

namespace Amlak.Rest.EndPoints;

/// <summary>Endpoints عمومی آگهی‌ها — فقط آگهی‌های منتشرشده، بدون نیاز به ورود</summary>
public class PropertyEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(ListingConstants.Routes.PublicBase)
            .WithApiVersionSet()
            .WithTags(ListingConstants.Tag)
            .AllowAnonymous();

        group.MapGetEndpoint<PagedResult<PropertyCard>>(   ListingConstants.Routes.Search,    Search,    ListingConstants.Names.Search,    ListingConstants.Docs.Search.Summary,    ListingConstants.Docs.Search.Description);
        group.MapGetEndpoint<PropertyDetail>(              ListingConstants.Routes.BySlug,    BySlug,    ListingConstants.Names.BySlug,    ListingConstants.Docs.BySlug.Summary,    ListingConstants.Docs.BySlug.Description);
        group.MapGetEndpoint<IReadOnlyList<PropertyCard>>( ListingConstants.Routes.Similar,   Similar,   ListingConstants.Names.Similar,   ListingConstants.Docs.Similar.Summary,   ListingConstants.Docs.Similar.Description);
        group.MapGetEndpoint<IReadOnlyList<LocationItem>>( ListingConstants.Routes.Locations, Locations, ListingConstants.Names.Locations, ListingConstants.Docs.Locations.Summary, ListingConstants.Docs.Locations.Description);
        group.MapGetEndpoint<IReadOnlyList<SitemapEntry>>( ListingConstants.Routes.Sitemap,   Sitemap,   ListingConstants.Names.Sitemap,   ListingConstants.Docs.Sitemap.Summary,   ListingConstants.Docs.Sitemap.Description);
    }

    private static async Task<PagedResult<PropertyCard>> Search([AsParameters] SearchPropertiesQuery query, IQueryBus bus, CancellationToken ct)
        => await bus.DispatchAsync<PagedResult<PropertyCard>>(query, ct);

    private static async Task<IResult> BySlug(string slug, IQueryBus bus, CancellationToken ct)
        => (await bus.DispatchAsync<PropertyDetail?>(new GetPropertyBySlugQuery(slug), ct)).OkOrNotFound();

    private static async Task<IReadOnlyList<PropertyCard>> Similar(long id, IQueryBus bus, CancellationToken ct, int take = 4)
        => await bus.DispatchAsync<IReadOnlyList<PropertyCard>>(new GetSimilarPropertiesQuery(id, take), ct);

    private static async Task<IReadOnlyList<LocationItem>> Locations(IQueryBus bus, CancellationToken ct)
        => await bus.DispatchAsync<IReadOnlyList<LocationItem>>(new GetLocationsQuery(), ct);

    private static async Task<IReadOnlyList<SitemapEntry>> Sitemap(IQueryBus bus, CancellationToken ct)
        => await bus.DispatchAsync<IReadOnlyList<SitemapEntry>>(new GetSitemapEntriesQuery(), ct);
}
