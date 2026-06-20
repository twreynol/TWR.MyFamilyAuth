namespace TWR.MyFamilyAuth.Contracts.DTOs.Groups;

public record FamilyGroupDto(
    Guid              Id,
    string            Name,
    Guid?             ParentGroupId,
    bool              IsActive,
    DateTime          CreatedAt,
    List<GroupMemberDto> Members
);

public record GroupMemberDto(
    Guid   UserId,
    string FullName,
    string Email,
    string GroupRole,
    bool   IsLimitedMember,
    DateTime JoinedAt
);
