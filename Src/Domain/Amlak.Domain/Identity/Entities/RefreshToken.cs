namespace Amlak.Domain.Identity.Entities;

/// <summary>موجودیت Refresh Token</summary>
public sealed class RefreshToken
{
    public long     Id        { get; private set; }
    public long     UserId    { get; private set; }
    public string   Token     { get; private set; } = string.Empty;
    public bool     IsRevoked { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private RefreshToken() { }

    public RefreshToken(long id, long userId, string token, DateTime expiresAt)
    {
        Id        = id;
        UserId    = userId;
        Token     = token;
        ExpiresAt = expiresAt;
        CreatedAt = DateTime.UtcNow;
    }

    public void Revoke() => IsRevoked = true;

    public bool IsValid() => !IsRevoked && DateTime.UtcNow <= ExpiresAt;
}
