namespace TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;
public record RegisterOptionsResponse(
    string ChallengeToken,
    string OptionsJson  // serialized Fido2NetLib CredentialCreateOptions — pass straight to navigator.credentials.create()
);
