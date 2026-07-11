using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    // WebAuthn credentials
    Task<WebAuthnCredential>       CreateWebAuthnCredentialAsync(WebAuthnCredential credential);
    Task<List<WebAuthnCredential>> GetWebAuthnCredentialsByUserAndRpIdAsync(Guid familyUserId, string rpId);
    Task<WebAuthnCredential?>      GetWebAuthnCredentialByCredentialIdAsync(string credentialId);
    Task                           UpdateWebAuthnCredentialSignCountAsync(Guid credentialRecordId, long signCount);
    Task<bool>                     DeleteWebAuthnCredentialAsync(Guid credentialRecordId, Guid familyUserId);

    // WebAuthn challenges
    Task<WebAuthnChallenge>  CreateWebAuthnChallengeAsync(WebAuthnChallenge challenge);
    Task<WebAuthnChallenge?> GetWebAuthnChallengeAsync(string challengeToken);
    Task                     MarkWebAuthnChallengeUsedAsync(Guid challengeId);
}
