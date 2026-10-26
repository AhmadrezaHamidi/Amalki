namespace Amlak.Application.Query.Publishing;

public sealed class PublishingQueryHandlers(ChannelReadRepository repo) :
    IQueryHandler<GetPropertyChannelPostsQuery, IReadOnlyList<ChannelPostItem>>,
    IQueryHandler<GetFailedChannelPostsQuery, IReadOnlyList<ChannelPostItem>>
{
    public Task<IReadOnlyList<ChannelPostItem>> HandleAsync(GetPropertyChannelPostsQuery query, CancellationToken ct)
        => repo.GetByPropertyAsync(query.PropertyId, ct);

    public Task<IReadOnlyList<ChannelPostItem>> HandleAsync(GetFailedChannelPostsQuery query, CancellationToken ct)
        => repo.GetFailedAsync(ct);
}
