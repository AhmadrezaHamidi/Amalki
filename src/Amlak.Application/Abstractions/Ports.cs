namespace Amlak.Application.Abstractions;

// Ports: what the use cases need from the outside world. The web project implements them with EF Core.
// Repositories load and store whole aggregates; reads for pages go through the read side (SQL views), not here.

/// <summary>Commits everything the use case changed, together with the outbox rows of the raised domain events.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
}

public interface IListingRepository
{
    /// <summary>The listing with its photos and messenger posts (what the aggregate's rules and its delete need), or null.</summary>
    Task<Property?> GetAsync(int id, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    /// <summary>Listings this verified phone sent in that still wait for an admin.</summary>
    Task<int> CountPendingByOwnerAsync(string ownerPhone, CancellationToken ct = default);
    void Add(Property listing);
    void Remove(Property listing);
}

public interface ILeadRepository
{
    Task<Lead?> GetAsync(int id, CancellationToken ct = default);
    void Add(Lead lead);
    void Remove(Lead lead);
}

public interface ITicketRepository
{
    /// <summary>The ticket with its messages, or null.</summary>
    Task<Ticket?> GetAsync(int id, CancellationToken ct = default);
    /// <summary>The ticket with its messages, or null. <paramref name="code"/> is already normalized.</summary>
    Task<Ticket?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken ct = default);
    void Add(Ticket ticket);
}
