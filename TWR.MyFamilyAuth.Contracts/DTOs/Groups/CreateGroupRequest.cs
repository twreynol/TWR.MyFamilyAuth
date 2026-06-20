namespace TWR.MyFamilyAuth.Contracts.DTOs.Groups;
public record CreateGroupRequest(string Name, Guid? ParentGroupId = null);
