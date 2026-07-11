namespace TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;
public record WebAuthnLoginOptionsResponse(
    string ChallengeToken,
    string OptionsJson  // serialized Fido2NetLib AssertionOptions — pass straight to navigator.credentials.get()
);
