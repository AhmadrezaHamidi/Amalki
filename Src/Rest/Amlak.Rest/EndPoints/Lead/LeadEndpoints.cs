using Amlak.Rest.EndPoints.Lead;

namespace Amlak.Rest.EndPoints;

/// <summary>ثبت درخواست تماس — عمومی و بدون ورود</summary>
public class LeadEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGroup(LeadConstants.Routes.PublicBase)
           .WithApiVersionSet()
           .WithTags(LeadConstants.Tag)
           .AllowAnonymous()
           .MapPostEndpoint<long>(LeadConstants.Routes.Submit, Post, LeadConstants.Names.Submit, LeadConstants.Docs.Submit.Summary, LeadConstants.Docs.Submit.Description);
    }

    private static async Task<long> Post([FromBody] SubmitLeadCommand cmd, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<long>(cmd, ct);
}

/// <summary>پیگیری درخواست‌های تماس — فقط مدیر</summary>
public class AdminLeadEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(LeadConstants.Routes.AdminBase)
            .WithApiVersionSet()
            .WithTags(LeadConstants.AdminTag)
            .RequireRoles(AmlakRoles.Admin);

        group.MapGetEndpoint<PagedResult<LeadItem>>(        LeadConstants.Routes.GetPaged, Gets,      LeadConstants.Names.GetPaged, LeadConstants.Docs.GetPaged.Summary, LeadConstants.Docs.GetPaged.Description);
        group.MapGetEndpoint<IReadOnlyList<LeadStatItem>>(  LeadConstants.Routes.Stats,    GetStats,  LeadConstants.Names.Stats,    LeadConstants.Docs.Stats.Summary,    LeadConstants.Docs.Stats.Description);
        group.MapGetEndpoint<LeadItem>(                     LeadConstants.Routes.GetById,  Get,       LeadConstants.Names.GetById,  LeadConstants.Docs.GetById.Summary,  LeadConstants.Docs.GetById.Description);
        group.MapPutEndpoint(                               LeadConstants.Routes.Status,   PutStatus, LeadConstants.Names.Status,   LeadConstants.Docs.Status.Summary,   LeadConstants.Docs.Status.Description);
    }

    private static async Task<PagedResult<LeadItem>> Gets([AsParameters] GetLeadsQuery query, IQueryBus bus, CancellationToken ct)
        => await bus.DispatchAsync<PagedResult<LeadItem>>(query, ct);

    private static async Task<IReadOnlyList<LeadStatItem>> GetStats(IQueryBus bus, CancellationToken ct)
        => await bus.DispatchAsync<IReadOnlyList<LeadStatItem>>(new GetLeadStatsQuery(), ct);

    private static async Task<IResult> Get(long id, IQueryBus bus, CancellationToken ct)
        => (await bus.DispatchAsync<LeadItem?>(new GetLeadQuery(id), ct)).OkOrNotFound();

    private static async Task<bool> PutStatus(long id, [FromBody] ChangeLeadStatusCommand cmd, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(cmd with { Id = id }, ct);
}
