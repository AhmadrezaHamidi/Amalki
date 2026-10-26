using AhmadBase.Persistence.NHiLoHelper;
using Amlak.Domain.Lead.Aggregates;
using Amlak.Domain.Lead.Repositories;
using Amlak.Domain.Listing.Aggregates;
using Amlak.Domain.Listing.Enums;
using Amlak.Domain.Listing.Repositories;
using Amlak.Domain.Publishing.Aggregates;
using Amlak.Domain.Publishing.Repositories;

namespace Amlak.Persistence.EF.Repositories;

public sealed class PropertyRepository(AmlakDbContext db, IHiLoIdGenerator hiLo) : IPropertyRepository
{
    public Task<Property?> GetByIdAsync(long id, CancellationToken ct = default)
        => db.Properties.Include(p => p.Photos).FirstOrDefaultAsync(p => p.Id == id, ct);

    // آگهی‌های حذف‌شده هم شمرده می‌شوند — ایندکس یکتا روی Slug شامل آن‌ها هم هست
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => db.Properties.IgnoreQueryFilters().AnyAsync(p => p.Slug == slug, ct);

    public Task<bool> IsPublishedAsync(long id, CancellationToken ct = default)
        => db.Properties.AnyAsync(p => p.Id == id && p.Status == ListingStatus.Published, ct);

    public async Task AddAsync(Property property, CancellationToken ct = default)
        => await db.Properties.AddAsync(property, ct);

    public Task UpdateAsync(Property property, CancellationToken ct = default)
    {
        db.Properties.Update(property);
        return Task.CompletedTask;
    }

    public Task<long> GetNextIdAsync() => Task.FromResult(hiLo.GetNextId<Property>());

    // HiLo فقط برای Aggregate Root است؛ برای موجودیت‌های فرزند از max+1 استفاده می‌شود
    public async Task<long> GetNextPhotoIdAsync()
        => (await db.PropertyPhotos.MaxAsync(p => (long?)p.Id) ?? 0) + 1;
}

public sealed class ChannelPostRepository(AmlakDbContext db, IHiLoIdGenerator hiLo) : IChannelPostRepository
{
    public Task<List<ChannelPost>> GetByPropertyAsync(long propertyId, CancellationToken ct = default)
        => db.ChannelPosts.Where(c => c.PropertyId == propertyId).ToListAsync(ct);

    public async Task AddAsync(ChannelPost post, CancellationToken ct = default)
        => await db.ChannelPosts.AddAsync(post, ct);

    public Task RemoveAsync(ChannelPost post, CancellationToken ct = default)
    {
        db.ChannelPosts.Remove(post);
        return Task.CompletedTask;
    }

    public Task<long> GetNextIdAsync() => Task.FromResult(hiLo.GetNextId<ChannelPost>());
}

public sealed class LeadRepository(AmlakDbContext db, IHiLoIdGenerator hiLo) : ILeadRepository
{
    public Task<ContactLead?> GetByIdAsync(long id, CancellationToken ct = default)
        => db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);

    public Task<int> CountRecentByPhoneAsync(string phoneNumber, TimeSpan window, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - window;
        return db.Leads.CountAsync(l => l.PhoneNumber == phoneNumber && l.CreatedAt > since, ct);
    }

    public async Task AddAsync(ContactLead lead, CancellationToken ct = default)
        => await db.Leads.AddAsync(lead, ct);

    public Task UpdateAsync(ContactLead lead, CancellationToken ct = default)
    {
        db.Leads.Update(lead);
        return Task.CompletedTask;
    }

    public Task<long> GetNextIdAsync() => Task.FromResult(hiLo.GetNextId<ContactLead>());
}
