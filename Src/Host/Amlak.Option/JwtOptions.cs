using AhmadBase.Context;

namespace Amlak.Option;

public class JWTOptions : IOption
{
    public string? ValidAudience { get; set; }
    public string? ValidIssuer { get; set; }
    public string? Secret { get; set; }
    public int TokenExpireMinutes { get; set; }
    public int RefreshExpireMinutes { get; set; }
}
