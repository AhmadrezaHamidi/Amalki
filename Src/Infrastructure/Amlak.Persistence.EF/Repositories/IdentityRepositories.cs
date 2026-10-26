using AhmadBase.Persistence.NHiLoHelper;
using Amlak.Domain.Identity.Aggregates;
using Amlak.Domain.Identity.Entities;
using Amlak.Domain.Identity.Repositories;

namespace Amlak.Persistence.EF.Repositories;

public sealed class UserRepository(AmlakDbContext db, IHiLoIdGenerator hiLo) : IUserRepository
{
    public Task<User?> GetByIdAsync(long id, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByPhoneAsync(string phoneNumber, CancellationToken ct = default)
        => db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phoneNumber, ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
        => await db.Users.AddAsync(user, ct);

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        db.Users.Update(user);
        return Task.CompletedTask;
    }

    public Task<long> GetNextIdAsync() => Task.FromResult(hiLo.GetNextId<User>());
}

public sealed class OtpRepository(AmlakDbContext db) : IOtpRepository
{
    public Task<OtpRequest?> GetLatestByPhoneAsync(string phoneNumber, CancellationToken ct = default)
        => db.OtpRequests
            .Where(o => o.PhoneNumber == phoneNumber && !o.IsUsed && o.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(o => o.Id)
            .FirstOrDefaultAsync(ct);

    public Task<bool> HasRecentAsync(string phoneNumber, TimeSpan window, CancellationToken ct = default)
    {
        var since = DateTime.UtcNow - window;
        return db.OtpRequests.AnyAsync(o => o.PhoneNumber == phoneNumber && o.CreatedAt > since, ct);
    }

    public async Task AddAsync(OtpRequest otp, CancellationToken ct = default)
        => await db.OtpRequests.AddAsync(otp, ct);

    public Task UpdateAsync(OtpRequest otp, CancellationToken ct = default)
    {
        db.OtpRequests.Update(otp);
        return Task.CompletedTask;
    }

    public async Task<long> GetNextIdAsync()
        => (await db.OtpRequests.MaxAsync(o => (long?)o.Id) ?? 0) + 1;
}

public sealed class RefreshTokenRepository(AmlakDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        => db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
        => await db.RefreshTokens.AddAsync(token, ct);

    public Task UpdateAsync(RefreshToken token, CancellationToken ct = default)
    {
        db.RefreshTokens.Update(token);
        return Task.CompletedTask;
    }

    public async Task<long> GetNextIdAsync()
        => (await db.RefreshTokens.MaxAsync(r => (long?)r.Id) ?? 0) + 1;
}
