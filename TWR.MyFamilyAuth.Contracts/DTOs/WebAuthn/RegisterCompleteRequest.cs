namespace TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;
public record RegisterCompleteRequest(
    string  ChallengeToken,
    string  AttestationResponseJson,  // JSON from navigator.credentials.create()'s result
    string? DeviceLabel = null
);
