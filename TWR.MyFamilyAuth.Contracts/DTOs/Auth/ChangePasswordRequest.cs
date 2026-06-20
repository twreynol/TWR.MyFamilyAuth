namespace TWR.MyFamilyAuth.Contracts.DTOs.Auth;
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
