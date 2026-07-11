namespace TWR.MyFamilyAuth.DAL.Entities;

public class WebAuthnCredential
{
    public Guid     Id              { get; set; }
    public Guid     FamilyUserId    { get; set; }
    public Guid     RegisteredAppId { get; set; }
    public string   RpId            { get; set; } = string.Empty; // hostname this credential is bound to
    public string   CredentialId    { get; set; } = string.Empty; // Base64Url, unique
    public string   PublicKey       { get; set; } = string.Empty; // Base64 COSE key blob
    public long     SignCount       { get; set; }                // uint from Fido2NetLib, stored widened
    public string   UserHandle      { get; set; } = string.Empty; // Base64Url random 32 bytes — not the FamilyUserId
    public Guid?    AaGuid          { get; set; }
    public string   Transports      { get; set; } = "[]";         // JSON array e.g. ["internal"]
    public string?  DeviceLabel     { get; set; }                 // user-facing e.g. "iPhone Face ID"
    public DateTime CreatedAt       { get; set; }
    public DateTime LastUsedAt      { get; set; }

    public FamilyUser    User { get; set; } = null!;
    public RegisteredApp App  { get; set; } = null!;
}
