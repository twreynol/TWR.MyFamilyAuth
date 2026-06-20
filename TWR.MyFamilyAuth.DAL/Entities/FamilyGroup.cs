namespace TWR.MyFamilyAuth.DAL.Entities;

public class FamilyGroup
{
    public Guid      Id            { get; set; }
    public string    Name          { get; set; } = string.Empty;
    public Guid?     ParentGroupId { get; set; }
    public bool      IsActive      { get; set; } = true;
    public DateTime  CreatedAt     { get; set; }
    public DateTime? UpdatedAt     { get; set; }

    public FamilyGroup?       ParentGroup { get; set; }
    public List<FamilyGroup>  SubGroups   { get; set; } = [];
    public List<GroupMember>  Members     { get; set; } = [];
    public List<FamilyUser>   PrimaryUsers { get; set; } = [];
}
