using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    // Refresh tokens
    Task<RefreshToken>   StoreRefreshTokenAsync(RefreshToken token);
    Task<RefreshToken?>  GetRefreshTokenByHashAsync(string tokenHash);
    Task                 RevokeRefreshTokenAsync(Guid tokenId);
    Task                 RevokeAllRefreshTokensAsync(Guid familyUserId);

    // Password reset
    Task<PasswordResetToken>  CreatePasswordResetTokenAsync(PasswordResetToken token);
    Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token);
    Task                      MarkPasswordResetTokenUsedAsync(Guid tokenId);

    // Audit
    Task WriteAuditLogAsync(AuditLog entry);

    // Registered apps
    Task<RegisteredApp?>      GetRegisteredAppByClientIdAsync(string clientId);
    Task<List<RegisteredApp>> GetAllRegisteredAppsAsync();
    Task<RegisteredApp>       CreateRegisteredAppAsync(RegisteredApp app);
    Task<bool>                DeactivateRegisteredAppAsync(Guid id);
    Task<RegisteredApp?>      UpdateRegisteredAppAsync(Guid id, bool? isActive = null, bool? requires2Fa = null, string? supportedRoles = null, string? allowedOrigins = null);
}
