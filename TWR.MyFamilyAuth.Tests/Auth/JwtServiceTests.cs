using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using TWR.MyFamilyAuth.API.Models;
using TWR.MyFamilyAuth.API.Services;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.Tests.Auth;

public class JwtServiceTests
{
    private static readonly Guid UserId  = Guid.NewGuid();
    private static readonly Guid GroupId = Guid.NewGuid();

    private static JwtSettings Settings(int expiryMinutes = 60) => new()
    {
        Issuer                 = "test-issuer",
        Audience               = "test-audience",
        Secret                 = "test-secret-key-must-be-at-least-32-chars-long!",
        ExpiryMinutes          = expiryMinutes,
        RefreshTokenExpiryDays = 30
    };

    private static JwtService Build(int expiryMinutes = 60)
        => new(Options.Create(Settings(expiryMinutes)));

    private static FamilyUser MakeUser(Guid? primaryGroupId = null, string? timeZone = null) => new()
    {
        Id             = UserId,
        FirstName      = "Jane",
        LastName       = "Doe",
        Email          = "jane@example.com",
        Role           = FamilyRoles.FamilyAdmin,
        PrimaryGroupId = primaryGroupId,
        TimeZoneId     = timeZone
    };

    private static JwtSecurityToken Parse(string token)
        => new JwtSecurityTokenHandler().ReadJwtToken(token);

    // ── GenerateToken tests ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ContainsExpectedClaims()
    {
        var svc   = Build();
        var user  = MakeUser();
        var token = svc.GenerateToken(user);
        var parsed = Parse(token);

        Assert.Equal(UserId.ToString(), parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        Assert.Equal(user.Email,        parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email).Value);
        Assert.Equal(user.FirstName,    parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.GivenName).Value);
        Assert.Equal(user.LastName,     parsed.Claims.First(c => c.Type == JwtRegisteredClaimNames.FamilyName).Value);
        Assert.Contains(parsed.Claims, c => c.Type == ClaimTypes.Role && c.Value == FamilyRoles.FamilyAdmin);
    }

    [Fact]
    public void GenerateToken_WithAppClientId_ContainsAppClaim()
    {
        var svc    = Build();
        var token  = svc.GenerateToken(MakeUser(), appClientId: "myfinances");
        var parsed = Parse(token);
        Assert.Contains(parsed.Claims, c => c.Type == "app_client_id" && c.Value == "myfinances");
    }

    [Fact]
    public void GenerateToken_WithAppRole_ContainsAppRoleClaim()
    {
        var svc    = Build();
        var token  = svc.GenerateToken(MakeUser(), appClientId: "myfinances", appRole: "Owner");
        var parsed = Parse(token);
        Assert.Contains(parsed.Claims, c => c.Type == "app_role" && c.Value == "Owner");
    }

    [Fact]
    public void GenerateToken_WithPrimaryGroup_ContainsGroupClaim()
    {
        var svc    = Build();
        var token  = svc.GenerateToken(MakeUser(primaryGroupId: GroupId));
        var parsed = Parse(token);
        Assert.Contains(parsed.Claims, c => c.Type == "RegisteredGroupId" && c.Value == GroupId.ToString());
    }

    [Fact]
    public void GenerateToken_WithTimeZone_ContainsTzClaim()
    {
        var svc    = Build();
        var token  = svc.GenerateToken(MakeUser(timeZone: "America/Chicago"));
        var parsed = Parse(token);
        Assert.Contains(parsed.Claims, c => c.Type == "tz" && c.Value == "America/Chicago");
    }

    [Fact]
    public void GenerateToken_ExpiresAtCorrectTime()
    {
        var expiryMinutes = 90;
        var svc           = Build(expiryMinutes);
        var before        = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var token         = svc.GenerateToken(MakeUser());
        var after         = DateTime.UtcNow.AddMinutes(expiryMinutes);
        var parsed        = Parse(token);

        // ValidTo should be within a 5-second window
        Assert.True(parsed.ValidTo >= before.AddSeconds(-5));
        Assert.True(parsed.ValidTo <= after.AddSeconds(5));
    }

    // ── ValidateToken tests ─────────────────────────────────────────────────────

    [Fact]
    public void ValidateToken_ValidToken_ReturnsValidAndPrincipal()
    {
        var svc   = Build();
        var token = svc.GenerateToken(MakeUser());

        var (valid, principal) = svc.ValidateToken(token);

        Assert.True(valid);
        Assert.NotNull(principal);
    }

    [Fact]
    public void ValidateToken_ExpiredToken_ReturnsFalse()
    {
        // ExpiryMinutes = -1 produces a token that expires 1 minute in the past
        var svc   = Build(expiryMinutes: -1);
        var token = svc.GenerateToken(MakeUser());

        // Use a fresh service with the same settings to validate
        var (valid, _) = svc.ValidateToken(token);
        Assert.False(valid);
    }

    [Fact]
    public void ValidateToken_TamperedToken_ReturnsFalse()
    {
        var svc   = Build();
        var token = svc.GenerateToken(MakeUser());

        // Tamper with the signature portion
        var parts    = token.Split('.');
        parts[2]     = Convert.ToBase64String(new byte[32]);
        var tampered = string.Join('.', parts);

        var (valid, _) = svc.ValidateToken(tampered);
        Assert.False(valid);
    }

    [Fact]
    public void ValidateToken_WrongSecret_ReturnsFalse()
    {
        var svc   = Build();
        var token = svc.GenerateToken(MakeUser());

        // Validate with a different secret
        var wrongSettings = Settings();
        wrongSettings.Secret = "completely-different-secret-key-that-is-long-enough!";
        var wrongSvc = new JwtService(Options.Create(wrongSettings));

        var (valid, _) = wrongSvc.ValidateToken(token);
        Assert.False(valid);
    }

    // ── HashToken tests ─────────────────────────────────────────────────────────

    [Fact]
    public void HashToken_SameInput_ProducesSameHash()
    {
        var svc   = Build();
        var input = "my-refresh-token";
        Assert.Equal(svc.HashToken(input), svc.HashToken(input));
    }

    [Fact]
    public void HashToken_DifferentInput_ProducesDifferentHash()
    {
        var svc = Build();
        Assert.NotEqual(svc.HashToken("token-a"), svc.HashToken("token-b"));
    }

    // ── GenerateRefreshToken tests ──────────────────────────────────────────────

    [Fact]
    public void GenerateRefreshToken_IsBase64AndNonEmpty()
    {
        var svc   = Build();
        var token = svc.GenerateRefreshToken();

        Assert.False(string.IsNullOrWhiteSpace(token));
        // Should be valid Base64 — no exception expected
        var bytes = Convert.FromBase64String(token);
        Assert.NotEmpty(bytes);
    }
}
