using Amlak.Domain.Lead.Aggregates;

namespace Amlak.Domain.Lead.Repositories;

public interface ILeadRepository
{
    Task<ContactLead?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<int>          CountRecentByPhoneAsync(string phoneNumber, TimeSpan window, CancellationToken ct = default);
    Task               AddAsync(ContactLead lead, CancellationToken ct = default);
    Task               UpdateAsync(ContactLead lead, CancellationToken ct = default);
    Task<long>         GetNextIdAsync();
}
