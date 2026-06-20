using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    // Two-factor challenges
    Task<TwoFactorChallenge>  CreateTwoFactorChallengeAsync(TwoFactorChallenge challenge);
    Task<TwoFactorChallenge?> GetTwoFactorChallengeAsync(string challengeToken);
    Task                      MarkChallengeUsedAsync(Guid challengeId);

    // Device trust
    Task<DeviceTrust>       CreateDeviceTrustAsync(DeviceTrust trust);
    Task<DeviceTrust?>      GetDeviceTrustByHashAsync(string tokenHash);
    Task                    UpdateDeviceTrustLastUsedAsync(Guid trustId);
    Task                    RevokeDeviceTrustAsync(Guid trustId);
    Task<List<DeviceTrust>> GetDeviceTrustsByUserAsync(Guid familyUserId);
}
