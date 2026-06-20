namespace TWR.MyFamilyAuth.DAL.Entities;

public class BuddyGrant
{
    public Guid      Id         { get; set; }
    public Guid      GrantorId  { get; set; }
    public Guid      GranteeId  { get; set; }
    public bool      IsActive   { get; set; } = true;
    public DateTime  GrantedAt  { get; set; }
    public DateTime? RevokedAt  { get; set; }

    public FamilyUser Grantor { get; set; } = null!;
    public FamilyUser Grantee { get; set; } = null!;
}
