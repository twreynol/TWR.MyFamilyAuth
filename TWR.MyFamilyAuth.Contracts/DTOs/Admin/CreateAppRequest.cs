namespace TWR.MyFamilyAuth.Contracts.DTOs.Admin;

public record CreateAppRequest(string Name, string AllowedOrigins, List<string>? SupportedRoles = null, string? ClientId = null);
