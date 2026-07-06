namespace TWR.MyFamilyAuth.Contracts.DTOs.BuddyGrants;

public record BuddyGrantDto(
    Guid        Id,
    Guid        GrantorId,
    string      GrantorName,
    string      GrantorEmail,
    Guid        GranteeId,
    string      GranteeName,
    string      GranteeEmail,
    string[]    Permissions,
    bool        IsActive,
    DateTime    GrantedAt,
    DateTime?   RevokedAt
);
