namespace TWR.MyFamilyAuth.Contracts.DTOs.Invitations;

public record InvitationDto(
    Guid      Id,
    string    InviteeEmail,
    string?   DisplayName,
    string    Token,
    Guid      InvitedByUserId,
    string    InvitedByName,
    DateTime  CreatedAt,
    DateTime  ExpiresAt,
    bool      IsAccepted,
    DateTime? AcceptedAt,
    bool      IsExpired
);
