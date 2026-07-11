namespace TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;
public record WebAuthnLoginCompleteRequest(
    string ChallengeToken,
    string AssertionResponseJson,  // JSON from navigator.credentials.get()'s result
    bool   RememberMe = false
);
