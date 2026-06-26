namespace TWR.MyFamilyAuth.Contracts.DTOs.BuddyGrants;

public record CreateBuddyGrantRequest(
    Guid     GranteeId,
    string[] Permissions   // e.g. ["Medical", "Info", "Messaging"]
);
