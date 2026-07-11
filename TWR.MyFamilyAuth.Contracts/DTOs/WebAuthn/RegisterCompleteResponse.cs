namespace TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;
public record RegisterCompleteResponse(
    Guid     CredentialRecordId,
    string   CredentialId,
    DateTime CreatedAt
);
