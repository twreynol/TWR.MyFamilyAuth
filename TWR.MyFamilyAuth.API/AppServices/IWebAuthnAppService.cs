using TWR.MyFamilyAuth.Contracts.DTOs.Auth;
using TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;

namespace TWR.MyFamilyAuth.API.AppServices;

public interface IWebAuthnAppService
{
    Task<RegisterOptionsResponse?>      GetRegisterOptionsAsync(Guid familyUserId, string appClientId, string? origin);
    Task<RegisterCompleteResponse?>     CompleteRegisterAsync(Guid familyUserId, string appClientId, string? origin, RegisterCompleteRequest request);
    Task<WebAuthnLoginOptionsResponse?> GetLoginOptionsAsync(WebAuthnLoginOptionsRequest request, string? origin);
    Task<LoginResponse?>                CompleteLoginAsync(WebAuthnLoginCompleteRequest request, string? origin, string? ipAddress);
    Task<List<PasskeyDto>?>             ListPasskeysAsync(Guid familyUserId, string appClientId, string? origin);
    Task<bool>                          DeletePasskeyAsync(Guid familyUserId, Guid credentialId);
}
