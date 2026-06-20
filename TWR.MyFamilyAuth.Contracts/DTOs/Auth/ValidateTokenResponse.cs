namespace TWR.MyFamilyAuth.Contracts.DTOs.Auth;
public record ValidateTokenResponse(
    bool    Valid,
    Guid?   UserId,
    string? Email,
    string? FullName,
    string? Role,
    Guid?   PrimaryGroupId,
    string? AppRole             // the user's role within this specific app
);
