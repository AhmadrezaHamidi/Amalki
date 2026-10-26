using Amlak.Rest.EndPoints.Publishing;

namespace Amlak.Rest.EndPoints;

/// <summary>Endpoints وضعیت کانال‌های تلگرام و بله — فقط مدیر</summary>
public class ChannelEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(PublishingConstants.Routes.BaseRoute)
            .WithApiVersionSet()
            .WithTags(PublishingConstants.Tag)
            .RequireRoles(AmlakRoles.Admin);

        group.MapGetEndpoint<IReadOnlyList<ChannelPostItem>>(PublishingConstants.Routes.ByProperty, GetByProperty, PublishingConstants.Names.ByProperty, PublishingConstants.Docs.ByProperty.Summary, PublishingConstants.Docs.ByProperty.Description);
        group.MapGetEndpoint<IReadOnlyList<ChannelPostItem>>(PublishingConstants.Routes.Failed,     GetFailed,     PublishingConstants.Names.Failed,     PublishingConstants.Docs.Failed.Summary,     PublishingConstants.Docs.Failed.Description);
        group.MapPostEndpoint<bool>(                         PublishingConstants.Routes.Sync,       PostSync,      PublishingConstants.Names.Sync,       PublishingConstants.Docs.Sync.Summary,       PublishingConstants.Docs.Sync.Description);
    }

    private static async Task<IReadOnlyList<ChannelPostItem>> GetByProperty(long id, IQueryBus bus, CancellationToken ct)
        => await bus.DispatchAsync<IReadOnlyList<ChannelPostItem>>(new GetPropertyChannelPostsQuery(id), ct);

    private static async Task<IReadOnlyList<ChannelPostItem>> GetFailed(IQueryBus bus, CancellationToken ct)
        => await bus.DispatchAsync<IReadOnlyList<ChannelPostItem>>(new GetFailedChannelPostsQuery(), ct);

    private static async Task<bool> PostSync(long id, ICommandBus bus, CancellationToken ct)
        => await bus.Dispatch<bool>(new SyncPropertyChannelsCommand(id), ct);
}
