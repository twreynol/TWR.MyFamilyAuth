namespace TWR.MyFamilyAuth.Contracts.DTOs.Users;
public record CreateUserRequest(
    string  FirstName,
    string  LastName,
    string  Email,
    string  Password,
    string  Role,
    Guid?   PrimaryGroupId = null,
    bool    MustChangePassword = false
);
