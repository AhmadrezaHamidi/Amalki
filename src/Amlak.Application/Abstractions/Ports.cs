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

public interface IConsultantRepository
{
    Task<Consultant?> GetAsync(int id, CancellationToken ct = default);
    void Add(Consultant consultant);
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

public interface IPhoneCodeRepository
{
    /// <summary>True when a code was issued for this number after <paramref name="sinceUtc"/>.</summary>
    Task<bool> IssuedSinceAsync(string phone, DateTime sinceUtc, CancellationToken ct = default);
    /// <summary>The newest unused, unexpired code for this number, or null.</summary>
    Task<PhoneCode?> LatestOpenAsync(string phone, DateTime nowUtc, CancellationToken ct = default);
    void Add(PhoneCode code);
}

// Adapters for these two live in Amlak.Infrastructure (Local / MinIO files, Log / Kavenegar SMS).

/// <summary>Where uploaded files live. Files are addressed by the public URL the store returned for them.</summary>
public interface IFileStore
{
    /// <summary>True when <paramref name="url"/> points into this store.</summary>
    bool Owns(string url);

    /// <returns>Public URL of the stored file.</returns>
    Task<string> PutAsync(string name, Stream content, long length, string contentType, CancellationToken ct = default);

    /// <returns>The file's bytes, or null when it does not exist.</returns>
    Task<Stream?> OpenAsync(string url, CancellationToken ct = default);

    Task DeleteAsync(string url, CancellationToken ct = default);
}

/// <summary>Sends SMS. Chosen by Sms:Provider: "Kavenegar", or "Log" (default, development).</summary>
public interface ISmsSender
{
    /// <summary>The one-time login code, through a template so it is never held back as advertising.</summary>
    Task<bool> SendCodeAsync(string mobile, string code, CancellationToken ct = default);

    /// <summary>Free text: alerts and campaigns. Callers go through the web SmsGateway, which adds the opt-out rules.</summary>
    Task<bool> SendTextAsync(string mobile, string text, CancellationToken ct = default);
}
