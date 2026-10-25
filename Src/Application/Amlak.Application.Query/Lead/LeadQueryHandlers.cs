namespace Amlak.Application.Query.Lead;

public sealed class LeadQueryHandlers(LeadReadRepository repo) :
    IQueryHandler<GetLeadsQuery, PagedResult<LeadItem>>,
    IQueryHandler<GetLeadQuery, LeadItem?>,
    IQueryHandler<GetLeadStatsQuery, IReadOnlyList<LeadStatItem>>
{
    public Task<PagedResult<LeadItem>> HandleAsync(GetLeadsQuery query, CancellationToken ct)
        => repo.GetPagedAsync(query.Page, query.PageSize, query.Status, query.PropertyId, ct);

    public Task<LeadItem?> HandleAsync(GetLeadQuery query, CancellationToken ct)
        => repo.GetByIdAsync(query.Id, ct);

    public Task<IReadOnlyList<LeadStatItem>> HandleAsync(GetLeadStatsQuery query, CancellationToken ct)
        => repo.GetStatsAsync(ct);
}
