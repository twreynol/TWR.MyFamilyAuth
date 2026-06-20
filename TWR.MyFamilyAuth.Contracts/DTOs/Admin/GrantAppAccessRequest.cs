namespace TWR.MyFamilyAuth.Contracts.DTOs.Admin;
public record GrantAppAccessRequest(Guid FamilyUserId, Guid RegisteredAppId, string? AppRole = null);
