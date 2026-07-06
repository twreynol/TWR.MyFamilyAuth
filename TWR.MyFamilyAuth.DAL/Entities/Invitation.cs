namespace TWR.MyFamilyAuth.DAL.Entities;

public class Invitation
{
    public Guid      Id              { get; set; }
    public Guid?     FamilyGroupId   { get; set; }   // V1 — nullable for V2 invitations
    public string    InviteeEmail    { get; set; } = string.Empty;
    public string?   DisplayName     { get; set; }   // V2 — e.g. "Reger Family"
    public string    Token           { get; set; } = string.Empty;
    public Guid      InvitedByUserId { get; set; }
    public DateTime  CreatedAt       { get; set; } = DateTime.UtcNow;
    public DateTime  ExpiresAt       { get; set; }
    public bool      IsAccepted      { get; set; }
    public DateTime? AcceptedAt      { get; set; }

    public FamilyGroup? Group     { get; set; }  // nullable — V2 invitations have no group
    public FamilyUser   InvitedBy { get; set; } = null!;
}
