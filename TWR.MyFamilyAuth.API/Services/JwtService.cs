using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TWR.MyFamilyAuth.API.Models;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.API.Services;

public class JwtService : IJwtService
{
    private readonly JwtSettings _settings;

    public JwtService(IOptions<JwtSettings> settings) => _settings = settings.Value;

    public string GenerateToken(FamilyUser user, IEnumerable<string>? permissions = null, string? appClientId = null, string? appRole = null)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub,        user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email,      user.Email),
            new(JwtRegisteredClaimNames.GivenName,  user.FirstName),
            new(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new(ClaimTypes.Role,                    user.Role),
        };
        if (user.PrimaryGroupId.HasValue)
            claims.Add(new("family_group_id", user.PrimaryGroupId.Value.ToString()));
        if (!string.IsNullOrEmpty(user.TimeZoneId))
            claims.Add(new("tz", user.TimeZoneId));
        if (!string.IsNullOrEmpty(appClientId))
            claims.Add(new("app_client_id", appClientId));
        if (!string.IsNullOrEmpty(appRole))
            claims.Add(new("app_role", appRole));
        // V2: one claim per distinct permission granted to this user by others
        foreach (var p in (permissions ?? []).Distinct())
            claims.Add(new("permissions", p));

        var token = new JwtSecurityToken(
            issuer:             _settings.Issuer,
            audience:           _settings.Audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddMinutes(_settings.ExpiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public (bool Valid, ClaimsPrincipal? Principal) ValidateToken(string token)
    {
        try
        {
            var key       = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
            var handler   = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer           = true,
                ValidateAudience         = true,
                ValidateLifetime         = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer              = _settings.Issuer,
                ValidAudience            = _settings.Audience,
                IssuerSigningKey         = key,
                ClockSkew                = TimeSpan.FromSeconds(30)
            }, out _);
            return (true, principal);
        }
        catch { return (false, null); }
    }
}
