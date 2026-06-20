namespace TWR.MyFamilyAuth.DAL.Entities;

public class Invitation
{
    public Guid      Id              { get; set; }
    public Guid      FamilyGroupId   { get; set; }
    public string    InviteeEmail    { get; set; } = string.Empty;
    public string    Token           { get; set; } = string.Empty;
    public Guid      InvitedByUserId { get; set; }
    public DateTime  CreatedAt       { get; set; }
    public DateTime  ExpiresAt       { get; set; }
    public bool      IsAccepted      { get; set; }
    public DateTime? AcceptedAt      { get; set; }

    public FamilyGroup Group      { get; set; } = null!;
    public FamilyUser  InvitedBy  { get; set; } = null!;
}
