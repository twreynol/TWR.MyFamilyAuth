using TWR.MyFamilyAuth.DAL.Entities;
namespace TWR.MyFamilyAuth.API.Services;
public interface IJwtService
{
    string GenerateToken(FamilyUser user, IEnumerable<string>? permissions = null, string? appClientId = null, string? appRole = null);
    string GenerateRefreshToken();
    string HashToken(string token);
    (bool Valid, System.Security.Claims.ClaimsPrincipal? Principal) ValidateToken(string token);
}
