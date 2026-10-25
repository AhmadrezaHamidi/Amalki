using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Amlak.Application.Contract.Services;
using Amlak.Domain.Identity.Aggregates;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Amlak.Persistence.EF.Services;

public sealed class JwtService(IConfiguration configuration) : IJwtService
{
    private readonly string _secret        = configuration["JWTOptions:Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
    private readonly string _validIssuer   = configuration["JWTOptions:ValidIssuer"] ?? "Amlak";
    private readonly string _validAudience = configuration["JWTOptions:ValidAudience"] ?? "AmlakClient";
    private readonly int    _expireMinutes = int.TryParse(configuration["JWTOptions:TokenExpireMinutes"], out var m) ? m : 60;

    public string GenerateAccessToken(User user)
    {
        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.MobilePhone,    user.PhoneNumber),
            new Claim(ClaimTypes.Role,           user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:             _validIssuer,
            audience:           _validAudience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_expireMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
