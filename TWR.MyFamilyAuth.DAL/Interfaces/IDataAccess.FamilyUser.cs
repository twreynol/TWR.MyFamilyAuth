using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    Task<FamilyUser?>        GetUserByIdAsync(Guid id);
    Task<FamilyUser?>        GetUserByEmailAsync(string email);
    Task<List<FamilyUser>>   GetAllUsersAsync(int page, int pageSize, string? search);
    Task<int>                GetUserCountAsync(string? search);
    Task<FamilyUser>         CreateUserAsync(FamilyUser user);
    Task<FamilyUser?>        UpdateUserAsync(FamilyUser user);
    Task                     UpdatePasswordAsync(Guid userId, string passwordHash);
    Task                     UpdateLastAccessedAsync(Guid userId);
    Task<bool>               DeactivateUserAsync(Guid id);
    Task<FamilyUser>         SeedSuperAdminAsync(string email, string firstName, string lastName, string passwordHash);
    Task<List<FamilyUser>>   GetWardsByGuardianAsync(Guid guardianId);
}
