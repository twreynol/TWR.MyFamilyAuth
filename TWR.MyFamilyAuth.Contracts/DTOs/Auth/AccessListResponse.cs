namespace TWR.MyFamilyAuth.Contracts.DTOs.Auth;

public record AccessListResponse(
    Guid   UserId,
    string AppClientId,
    Guid[] GrantorIds,
    Guid[] WardIds,
    Guid[] AccessList,  // GrantorIds + WardIds, deduplicated
    Dictionary<Guid, string>? GrantorEmails = null  // grantor id -> email, so callers can resolve local identities without needing an MFA-id mapping
);
