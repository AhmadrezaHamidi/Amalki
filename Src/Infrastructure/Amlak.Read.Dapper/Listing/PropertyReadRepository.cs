using Amlak.Domain.Listing.Enums;

namespace Amlak.Read.Dapper.Listing;

public sealed class PropertyReadRepository(IDbConnection conn, PhotoUrls photoUrls)
{
    private const string CardColumns = """
        p.Id, p.Slug, p.Title, p.Deal, p.Kind, p.Price, p.Deposit, p.MonthlyRent, p.AreaM2, p.Bedrooms,
        p.City, p.District,
        (SELECT TOP 1 @PhotoBase + ph.FileName FROM PropertyPhotos ph WHERE ph.PropertyId = p.Id ORDER BY ph.SortOrder) AS CoverPhotoUrl,
        p.PublishedAt
        """;

    public async Task<PagedResult<PropertyCard>> SearchAsync(SearchPropertiesQuery q, CancellationToken ct = default)
    {
        var (page, pageSize) = Paging.Normalize(q.Page, q.PageSize);
        var where = new List<string> { "p.IsDeleted = 0", "p.Status = 'Published'" };
        var param = new DynamicParameters(new { PhotoBase = photoUrls.Prefix });

        if (q.Deal.HasValue)                        { where.Add("p.Deal = @Deal");                          param.Add("Deal", q.Deal.Value.ToString()); }
        if (q.Kind.HasValue)                        { where.Add("p.Kind = @Kind");                          param.Add("Kind", q.Kind.Value.ToString()); }
        if (!string.IsNullOrWhiteSpace(q.City))     { where.Add("p.City = @City");                          param.Add("City", q.City.Trim()); }
        if (!string.IsNullOrWhiteSpace(q.District)) { where.Add("p.District = @District");                  param.Add("District", q.District.Trim()); }
        if (q.MinPrice.HasValue)                    { where.Add("COALESCE(p.Price, p.Deposit) >= @MinPrice"); param.Add("MinPrice", q.MinPrice); }
        if (q.MaxPrice.HasValue)                    { where.Add("COALESCE(p.Price, p.Deposit) <= @MaxPrice"); param.Add("MaxPrice", q.MaxPrice); }
        if (q.MaxRent.HasValue)                     { where.Add("p.MonthlyRent <= @MaxRent");               param.Add("MaxRent", q.MaxRent); }
        if (q.MinArea.HasValue)                     { where.Add("p.AreaM2 >= @MinArea");                    param.Add("MinArea", q.MinArea); }
        if (q.MaxArea.HasValue)                     { where.Add("p.AreaM2 <= @MaxArea");                    param.Add("MaxArea", q.MaxArea); }
        if (q.MinBedrooms.HasValue)                 { where.Add("p.Bedrooms >= @MinBedrooms");              param.Add("MinBedrooms", q.MinBedrooms); }

        // ترتیب فقط از فهرست سفید — هرگز از ورودی خام در SQL
        var orderBy = q.Sort switch
        {
            ListingSort.PriceAsc  => "COALESCE(p.Price, p.Deposit) ASC, p.PublishedAt DESC",
            ListingSort.PriceDesc => "COALESCE(p.Price, p.Deposit) DESC, p.PublishedAt DESC",
            ListingSort.AreaDesc  => "p.AreaM2 DESC, p.PublishedAt DESC",
            _                     => "p.PublishedAt DESC, p.Id DESC",
        };

        var whereClause = "WHERE " + string.Join(" AND ", where);
        param.Add("Offset",   (page - 1) * pageSize);
        param.Add("PageSize", pageSize);

        var total = await conn.QuerySingleAsync<int>($"SELECT COUNT(*) FROM Properties p {whereClause}", param);
        var items = (await conn.QueryAsync<PropertyCard>($"""
            SELECT {CardColumns}
            FROM Properties p {whereClause}
            ORDER BY {orderBy}
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, param)).ToList();

        return new PagedResult<PropertyCard>(items, total, page, pageSize);
    }

    /// <summary>نسخهٔ عمومی: آدرس دقیق فقط در پنل مدیر است و به بازدیدکننده داده نمی‌شود</summary>
    public async Task<PropertyDetail?> GetPublishedBySlugAsync(string slug, CancellationToken ct = default)
        => await GetDetailAsync("p.Slug = @Key AND p.Status = 'Published'", slug) is { } d ? d with { Address = null } : null;

    public Task<PropertyDetail?> GetByIdAsync(long id, CancellationToken ct = default)
        => GetDetailAsync("p.Id = @Key", id);

    public async Task<IReadOnlyList<PropertyCard>> GetSimilarAsync(long propertyId, int take, CancellationToken ct = default)
    {
        var sql = $"""
            SELECT TOP (@Take) {CardColumns}
            FROM Properties p
            INNER JOIN Properties src ON src.Id = @PropertyId
            WHERE p.IsDeleted = 0 AND p.Status = 'Published' AND p.Id <> src.Id
              AND p.City = src.City AND p.Deal = src.Deal
            ORDER BY CASE WHEN p.District = src.District THEN 0 ELSE 1 END,
                     CASE WHEN p.Kind = src.Kind THEN 0 ELSE 1 END,
                     p.PublishedAt DESC
            """;
        var result = await conn.QueryAsync<PropertyCard>(sql, new { PhotoBase = photoUrls.Prefix, PropertyId = propertyId, Take = Math.Clamp(take, 1, 12) });
        return result.ToList();
    }

    public async Task<IReadOnlyList<LocationItem>> GetLocationsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.City, p.District, COUNT(*) AS Count
            FROM Properties p
            WHERE p.IsDeleted = 0 AND p.Status = 'Published'
            GROUP BY p.City, p.District
            ORDER BY p.City, COUNT(*) DESC
            """;
        return (await conn.QueryAsync<LocationItem>(sql)).ToList();
    }

    public async Task<IReadOnlyList<SitemapEntry>> GetSitemapAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT p.Slug, p.Title, p.UpdatedAt AS LastModified,
                   (SELECT TOP 1 @PhotoBase + ph.FileName FROM PropertyPhotos ph WHERE ph.PropertyId = p.Id ORDER BY ph.SortOrder) AS CoverPhotoUrl
            FROM Properties p
            WHERE p.IsDeleted = 0 AND p.Status = 'Published'
            ORDER BY p.UpdatedAt DESC
            """;
        return (await conn.QueryAsync<SitemapEntry>(sql, new { PhotoBase = photoUrls.Prefix })).ToList();
    }

    public async Task<PagedResult<PropertyAdminListItem>> GetAdminPagedAsync(
        int page, int pageSize, ListingStatus? status, string? search, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var where = new List<string> { "p.IsDeleted = 0" };
        var param = new DynamicParameters();

        if (status.HasValue)                   { where.Add("p.Status = @Status"); param.Add("Status", status.Value.ToString()); }
        if (!string.IsNullOrWhiteSpace(search)) { where.Add("(p.Title LIKE @Search OR p.District LIKE @Search OR p.Slug LIKE @Search)"); param.Add("Search", $"%{search.Trim()}%"); }

        var whereClause = "WHERE " + string.Join(" AND ", where);
        param.Add("Offset",   (page - 1) * pageSize);
        param.Add("PageSize", pageSize);

        var total = await conn.QuerySingleAsync<int>($"SELECT COUNT(*) FROM Properties p {whereClause}", param);
        var items = (await conn.QueryAsync<PropertyAdminListItem>($"""
            SELECT p.Id, p.Slug, p.Title, p.Deal, p.Status, p.City, p.District,
                   (SELECT COUNT(*) FROM PropertyPhotos ph WHERE ph.PropertyId = p.Id) AS PhotoCount,
                   (SELECT COUNT(*) FROM Leads l WHERE l.PropertyId = p.Id AND l.Status = 'New' AND l.IsDeleted = 0) AS NewLeadCount,
                   CAST(CASE WHEN EXISTS (SELECT 1 FROM ChannelPosts c WHERE c.PropertyId = p.Id AND c.Error IS NOT NULL) THEN 1 ELSE 0 END AS bit) AS HasChannelError,
                   p.CreatedAt, p.PublishedAt
            FROM Properties p {whereClause}
            ORDER BY p.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, param)).ToList();

        return new PagedResult<PropertyAdminListItem>(items, total, page, pageSize);
    }

    private async Task<PropertyDetail?> GetDetailAsync(string condition, object key)
    {
        var sql = $"""
            SELECT p.Id, p.Slug, p.Title, p.Description, p.Deal, p.Kind, p.Status,
                   p.Price, p.Deposit, p.MonthlyRent, p.AreaM2, p.Bedrooms, p.Floor, p.Parking, p.YearBuilt,
                   p.City, p.District, p.Address, p.Latitude, p.Longitude,
                   p.CreatedAt, p.PublishedAt, p.UpdatedAt
            FROM Properties p
            WHERE p.IsDeleted = 0 AND {condition};

            SELECT ph.Id, @PhotoBase + ph.FileName AS Url, ph.Alt, ph.SortOrder
            FROM PropertyPhotos ph
            INNER JOIN Properties p ON p.Id = ph.PropertyId
            WHERE p.IsDeleted = 0 AND {condition}
            ORDER BY ph.SortOrder;
            """;

        using var multi = await conn.QueryMultipleAsync(sql, new { Key = key, PhotoBase = photoUrls.Prefix });
        var row = await multi.ReadFirstOrDefaultAsync<DetailRow>();
        if (row is null) return null;

        var photos = (await multi.ReadAsync<PhotoItem>()).ToList();
        return new PropertyDetail(
            row.Id, row.Slug, row.Title, row.Description, row.Deal, row.Kind, row.Status,
            row.Price, row.Deposit, row.MonthlyRent, row.AreaM2, row.Bedrooms, row.Floor, row.Parking, row.YearBuilt,
            row.City, row.District, row.Address, row.Latitude, row.Longitude,
            row.CreatedAt, row.PublishedAt, row.UpdatedAt, photos);
    }

    private sealed record DetailRow(
        long Id, string Slug, string Title, string Description, string Deal, string Kind, string Status,
        long? Price, long? Deposit, long? MonthlyRent, int AreaM2, int? Bedrooms, int? Floor, int? Parking, int? YearBuilt,
        string City, string District, string? Address, double? Latitude, double? Longitude,
        DateTime CreatedAt, DateTime? PublishedAt, DateTime UpdatedAt);
}
