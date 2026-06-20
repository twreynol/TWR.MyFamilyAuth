namespace TWR.MyFamilyAuth.Contracts.DTOs.Groups;
public record AddGroupMemberRequest(Guid FamilyUserId, string GroupRole = "Member", bool IsLimitedMember = false);
