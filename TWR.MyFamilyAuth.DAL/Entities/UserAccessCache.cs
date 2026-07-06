namespace TWR.MyFamilyAuth.DAL.Entities;

public class UserAccessCache
{
    public Guid     UserId      { get; set; }
    public string   AppClientId { get; set; } = string.Empty;
    public Guid[]   GrantorIds  { get; set; } = [];
    public DateTime UpdatedAt   { get; set; }
}
