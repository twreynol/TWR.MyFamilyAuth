namespace TWR.MyFamilyAuth.DAL.Entities;

public class GroupMember
{
    public Guid     Id              { get; set; }
    public Guid     FamilyUserId    { get; set; }
    public Guid     FamilyGroupId   { get; set; }
    public string   GroupRole       { get; set; } = "Member"; // "Member", "Admin", "Owner"
    public bool     IsLimitedMember { get; set; }
    public DateTime JoinedAt        { get; set; }

    public FamilyUser  User  { get; set; } = null!;
    public FamilyGroup Group { get; set; } = null!;
}
