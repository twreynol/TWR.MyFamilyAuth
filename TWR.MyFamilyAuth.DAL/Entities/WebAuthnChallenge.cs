namespace TWR.MyFamilyAuth.DAL.Entities;

public class WebAuthnChallenge
{
    public Guid     Id              { get; set; }
    public Guid     FamilyUserId    { get; set; }
    public Guid     RegisteredAppId { get; set; }
    public string   RpId            { get; set; } = string.Empty;
    public string   ChallengeToken  { get; set; } = string.Empty; // random GUID, sent to client
    public string   ChallengeKind   { get; set; } = string.Empty; // "Registration" | "Assertion"
    public string   OptionsJson     { get; set; } = string.Empty; // serialized Fido2NetLib options — not a secret, sent to client verbatim
    public DateTime CreatedAt       { get; set; }
    public DateTime ExpiresAt       { get; set; }
    public bool     IsUsed          { get; set; }

    public FamilyUser    User { get; set; } = null!;
    public RegisteredApp App  { get; set; } = null!;
}
