namespace Amlak.Read.Dapper.Publishing;

public sealed class ChannelReadRepository(IDbConnection conn)
{
    private const string Select = """
        SELECT c.Id, c.PropertyId, p.Title AS PropertyTitle, c.Channel, c.ExternalMessageId, c.HasPhoto, c.Error,
               c.CreatedAt, c.LastSyncedAt
        FROM ChannelPosts c
        INNER JOIN Properties p ON p.Id = c.PropertyId
        """;

    public async Task<IReadOnlyList<ChannelPostItem>> GetByPropertyAsync(long propertyId, CancellationToken ct = default)
        => (await conn.QueryAsync<ChannelPostItem>($"{Select} WHERE c.PropertyId = @PropertyId ORDER BY c.Channel", new { PropertyId = propertyId })).ToList();

    public async Task<IReadOnlyList<ChannelPostItem>> GetFailedAsync(CancellationToken ct = default)
        => (await conn.QueryAsync<ChannelPostItem>($"{Select} WHERE c.Error IS NOT NULL AND p.IsDeleted = 0 ORDER BY c.Id DESC")).ToList();
}
