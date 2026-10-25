namespace Amlak.Application.Query.Listing;

public sealed class ListingQueryHandlers(PropertyReadRepository repo) :
    IQueryHandler<SearchPropertiesQuery, PagedResult<PropertyCard>>,
    IQueryHandler<GetPropertyBySlugQuery, PropertyDetail?>,
    IQueryHandler<GetSimilarPropertiesQuery, IReadOnlyList<PropertyCard>>,
    IQueryHandler<GetLocationsQuery, IReadOnlyList<LocationItem>>,
    IQueryHandler<GetSitemapEntriesQuery, IReadOnlyList<SitemapEntry>>,
    IQueryHandler<GetAdminPropertiesQuery, PagedResult<PropertyAdminListItem>>,
    IQueryHandler<GetPropertyQuery, PropertyDetail?>
{
    public Task<PagedResult<PropertyCard>> HandleAsync(SearchPropertiesQuery query, CancellationToken ct)
        => repo.SearchAsync(query, ct);

    public Task<PropertyDetail?> HandleAsync(GetPropertyBySlugQuery query, CancellationToken ct)
        => repo.GetPublishedBySlugAsync(query.Slug, ct);

    public Task<IReadOnlyList<PropertyCard>> HandleAsync(GetSimilarPropertiesQuery query, CancellationToken ct)
        => repo.GetSimilarAsync(query.PropertyId, query.Take, ct);

    public Task<IReadOnlyList<LocationItem>> HandleAsync(GetLocationsQuery query, CancellationToken ct)
        => repo.GetLocationsAsync(ct);

    public Task<IReadOnlyList<SitemapEntry>> HandleAsync(GetSitemapEntriesQuery query, CancellationToken ct)
        => repo.GetSitemapAsync(ct);

    public Task<PagedResult<PropertyAdminListItem>> HandleAsync(GetAdminPropertiesQuery query, CancellationToken ct)
        => repo.GetAdminPagedAsync(query.Page, query.PageSize, query.Status, query.Search, ct);

    public Task<PropertyDetail?> HandleAsync(GetPropertyQuery query, CancellationToken ct)
        => repo.GetByIdAsync(query.Id, ct);
}
