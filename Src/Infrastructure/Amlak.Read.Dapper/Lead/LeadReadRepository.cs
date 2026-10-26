using Amlak.Domain.Lead.Enums;

namespace Amlak.Read.Dapper.Lead;

public sealed class LeadReadRepository(IDbConnection conn)
{
    private const string Select = """
        SELECT l.Id, l.PropertyId, p.Title AS PropertyTitle, p.Slug AS PropertySlug,
               l.FullName, l.PhoneNumber, l.Message, l.Status, l.AdminNote, l.CreatedAt, l.HandledAt
        FROM Leads l
        LEFT JOIN Properties p ON p.Id = l.PropertyId
        """;

    public async Task<PagedResult<LeadItem>> GetPagedAsync(
        int page, int pageSize, LeadStatus? status, long? propertyId, CancellationToken ct = default)
    {
        (page, pageSize) = Paging.Normalize(page, pageSize);
        var where = new List<string> { "l.IsDeleted = 0" };
        var param = new DynamicParameters();

        if (status.HasValue)     { where.Add("l.Status = @Status");         param.Add("Status", status.Value.ToString()); }
        if (propertyId.HasValue) { where.Add("l.PropertyId = @PropertyId"); param.Add("PropertyId", propertyId); }

        var whereClause = "WHERE " + string.Join(" AND ", where);
        param.Add("Offset",   (page - 1) * pageSize);
        param.Add("PageSize", pageSize);

        var total = await conn.QuerySingleAsync<int>($"SELECT COUNT(*) FROM Leads l {whereClause}", param);
        var items = (await conn.QueryAsync<LeadItem>($"""
            {Select} {whereClause}
            ORDER BY l.CreatedAt DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY
            """, param)).ToList();

        return new PagedResult<LeadItem>(items, total, page, pageSize);
    }

    public Task<LeadItem?> GetByIdAsync(long id, CancellationToken ct = default)
        => conn.QueryFirstOrDefaultAsync<LeadItem>($"{Select} WHERE l.Id = @Id AND l.IsDeleted = 0", new { Id = id });

    public async Task<IReadOnlyList<LeadStatItem>> GetStatsAsync(CancellationToken ct = default)
        => (await conn.QueryAsync<LeadStatItem>("SELECT l.Status, COUNT(*) AS Count FROM Leads l WHERE l.IsDeleted = 0 GROUP BY l.Status")).ToList();
}
