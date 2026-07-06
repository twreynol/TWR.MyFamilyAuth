namespace TWR.MyFamilyAuth.Contracts.DTOs.Invitations;

public record CreateInvitationRequest(
    string  InviteeEmail,
    string? DisplayName    // e.g. "Reger Family" — shown on invite landing page
);
