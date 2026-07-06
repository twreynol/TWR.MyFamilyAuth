using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using TWR.MyFamilyAuth.API.Models;
using TWR.MyFamilyAuth.API.Services;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.Tests.BuddyGrants;

public class JwtPermissionsTests
{
    private static readonly Guid TimId = new("00000000-0000-0000-0000-000000000003");

    private static JwtService BuildJwtService() => new(Options.Create(new JwtSettings
    {
        Issuer                 = "test-issuer",
        Audience               = "test-audience",
        Secret                 = "test-secret-key-must-be-at-least-32-chars-long!",
        ExpiryMinutes          = 60,
        RefreshTokenExpiryDays = 30
    }));

    private static FamilyUser Tim() => new()
    {
        Id        = TimId,
        FirstName = "Tim",
        LastName  = "Reynolds",
        Email     = "twreynol@hotmail.com",
        Role      = FamilyRoles.SuperAdmin
    };

    [Fact]
    public void GenerateToken_IncludesPermissions_WhenGrantsExist()
    {
        var jwt   = BuildJwtService();
        var token = jwt.GenerateToken(Tim(), ["Medical", "Info", "Messaging"]);

        var handler = new JwtSecurityTokenHandler();
        var parsed  = handler.ReadJwtToken(token);
        var perms   = parsed.Claims.Where(c => c.Type == "permissions").Select(c => c.Value).ToList();

        Assert.Contains("Medical",   perms);
        Assert.Contains("Info",      perms);
        Assert.Contains("Messaging", perms);
    }

    [Fact]
    public void GenerateToken_HasNoPermissionsClaim_WhenNoGrantsExist()
    {
        var jwt   = BuildJwtService();
        var token = jwt.GenerateToken(Tim(), []);

        var handler = new JwtSecurityTokenHandler();
        var parsed  = handler.ReadJwtToken(token);
        var perms   = parsed.Claims.Where(c => c.Type == "permissions").ToList();

        Assert.Empty(perms);
    }

    [Fact]
    public void GenerateToken_DeduplicatesPermissions()
    {
        var jwt   = BuildJwtService();
        var token = jwt.GenerateToken(Tim(), ["Medical", "Medical", "Info"]);

        var handler = new JwtSecurityTokenHandler();
        var parsed  = handler.ReadJwtToken(token);
        var perms   = parsed.Claims.Where(c => c.Type == "permissions").Select(c => c.Value).ToList();

        Assert.Equal(2, perms.Count);
        Assert.Single(perms, p => p == "Medical");
    }

    [Fact]
    public void GenerateToken_BackwardCompat_StillIncludesV1Claims()
    {
        var jwt   = BuildJwtService();
        var token = jwt.GenerateToken(Tim(), ["Medical"], "myfinances", "Owner");

        var handler = new JwtSecurityTokenHandler();
        var parsed  = handler.ReadJwtToken(token);

        Assert.NotNull(parsed.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub));
        Assert.NotNull(parsed.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Email));
        Assert.NotNull(parsed.Claims.FirstOrDefault(c => c.Type == "app_client_id"));
        Assert.NotNull(parsed.Claims.FirstOrDefault(c => c.Type == "app_role"));
    }
}
