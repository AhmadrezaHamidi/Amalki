using Amlak.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Data;

// EF Core adapters for the Application ports. AppDbContext itself is the unit of work: its SaveChanges
// writes the outbox rows of the raised domain events in the same transaction.

public sealed class EfListingRepository(AppDbContext db) : IListingRepository
{
    public Task<Property?> GetAsync(int id, CancellationToken ct = default) =>
        db.Properties.Include(x => x.Photos).Include(x => x.ChannelPosts).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => db.Properties.AnyAsync(x => x.Slug == slug, ct);

    public Task<int> CountPendingByOwnerAsync(string ownerPhone, CancellationToken ct = default) =>
        db.Properties.CountAsync(p => p.OwnerPhone == ownerPhone && p.Status == ListingStatus.Pending, ct);

    public void Add(Property listing) => db.Properties.Add(listing);
    public void Remove(Property listing) => db.Properties.Remove(listing);
}

public sealed class EfLeadRepository(AppDbContext db) : ILeadRepository
{
    public async Task<Lead?> GetAsync(int id, CancellationToken ct = default) => await db.Leads.FindAsync([id], ct);
    public void Add(Lead lead) => db.Leads.Add(lead);
    public void Remove(Lead lead) => db.Leads.Remove(lead);
}

public sealed class EfTicketRepository(AppDbContext db) : ITicketRepository
{
    public Task<Ticket?> GetAsync(int id, CancellationToken ct = default) =>
        db.Tickets.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Id == id, ct);

    public Task<Ticket?> GetByCodeAsync(string code, CancellationToken ct = default) =>
        db.Tickets.Include(x => x.Messages).FirstOrDefaultAsync(x => x.Code == code, ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct = default) => db.Tickets.AnyAsync(t => t.Code == code, ct);

    public void Add(Ticket ticket) => db.Tickets.Add(ticket);
}

public sealed class EfPhoneCodeRepository(AppDbContext db) : IPhoneCodeRepository
{
    public Task<bool> IssuedSinceAsync(string phone, DateTime sinceUtc, CancellationToken ct = default) =>
        db.PhoneCodes.AnyAsync(x => x.Phone == phone && x.CreatedAtUtc > sinceUtc, ct);

    public Task<PhoneCode?> LatestOpenAsync(string phone, DateTime nowUtc, CancellationToken ct = default) =>
        db.PhoneCodes.Where(x => x.Phone == phone && !x.Used && x.ExpiresAtUtc > nowUtc)
            .OrderByDescending(x => x.CreatedAtUtc).FirstOrDefaultAsync(ct);

    public void Add(PhoneCode code) => db.PhoneCodes.Add(code);
}

public static class PersistenceRegistration
{
    /// <summary>Wires the Application layer: its use cases and the EF Core adapters for its ports.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<IListingRepository, EfListingRepository>();
        services.AddScoped<ILeadRepository, EfLeadRepository>();
        services.AddScoped<ITicketRepository, EfTicketRepository>();
        services.AddScoped<IPhoneCodeRepository, EfPhoneCodeRepository>();
        foreach (var useCase in UseCases.All) services.AddScoped(useCase);
        return services;
    }
}
