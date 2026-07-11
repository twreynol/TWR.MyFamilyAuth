namespace TWR.MyFamilyAuth.Contracts.DTOs.WebAuthn;

public record PasskeyDto(Guid Id, string? DeviceLabel, DateTime CreatedAt, DateTime LastUsedAt);
