using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    Task<AppAccess?>       GetAppAccessAsync(Guid familyUserId, Guid registeredAppId);
    Task<List<AppAccess>>  GetAppAccessByUserAsync(Guid familyUserId);
    Task<List<AppAccess>>  GetAppAccessByAppAsync(Guid registeredAppId);
    Task<AppAccess>        GrantAppAccessAsync(AppAccess access);
    Task<bool>             RevokeAppAccessAsync(Guid familyUserId, Guid registeredAppId, Guid revokedByUserId);
    Task                   GrantAppAccessToAllSuperAdminsAsync(Guid registeredAppId, Guid grantedByUserId);
}
