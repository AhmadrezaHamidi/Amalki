using Amlak.Domain.Publishing.Aggregates;

namespace Amlak.Domain.Publishing.Repositories;

public interface IChannelPostRepository
{
    Task<List<ChannelPost>> GetByPropertyAsync(long propertyId, CancellationToken ct = default);
    Task                    AddAsync(ChannelPost post, CancellationToken ct = default);
    Task                    RemoveAsync(ChannelPost post, CancellationToken ct = default);
    Task<long>              GetNextIdAsync();
}
