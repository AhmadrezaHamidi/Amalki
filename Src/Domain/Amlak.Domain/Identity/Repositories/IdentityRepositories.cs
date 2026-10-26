using Amlak.Domain.Identity.Aggregates;
using Amlak.Domain.Identity.Entities;

namespace Amlak.Domain.Identity.Repositories;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<User?> GetByPhoneAsync(string phoneNumber, CancellationToken ct = default);
    Task        AddAsync(User user, CancellationToken ct = default);
    Task        UpdateAsync(User user, CancellationToken ct = default);
    Task<long>  GetNextIdAsync();
}

public interface IOtpRepository
{
    Task<OtpRequest?> GetLatestByPhoneAsync(string phoneNumber, CancellationToken ct = default);
    Task<bool>        HasRecentAsync(string phoneNumber, TimeSpan window, CancellationToken ct = default);
    Task              AddAsync(OtpRequest otp, CancellationToken ct = default);
    Task              UpdateAsync(OtpRequest otp, CancellationToken ct = default);
    Task<long>        GetNextIdAsync();
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task                AddAsync(RefreshToken token, CancellationToken ct = default);
    Task                UpdateAsync(RefreshToken token, CancellationToken ct = default);
    Task<long>          GetNextIdAsync();
}
