using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    Task<UserAccessCache?> GetAccessCacheAsync(Guid userId, string appClientId);
    Task UpsertAccessCacheAsync(UserAccessCache cache);
    Task DeleteAccessCacheAsync(Guid userId);
}
