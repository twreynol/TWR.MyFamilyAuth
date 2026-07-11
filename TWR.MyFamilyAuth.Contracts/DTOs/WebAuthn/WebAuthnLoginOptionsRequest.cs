namespace TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;
public record WebAuthnLoginOptionsRequest(
    string Email,
    string AppClientId
);
