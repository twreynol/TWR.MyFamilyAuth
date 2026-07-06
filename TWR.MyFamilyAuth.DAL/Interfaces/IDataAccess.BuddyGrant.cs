using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    Task<List<BuddyGrant>> GetGrantsGivenAsync(Guid grantorId);        // V2 — replaces GetBuddyGrantsForUserAsync
    Task<List<BuddyGrant>> GetGrantsReceivedAsync(Guid granteeId);
    Task<BuddyGrant?>      GetGrantByIdAsync(Guid grantId);
    Task<BuddyGrant?>      GetGrantBetweenAsync(Guid grantorId, Guid granteeId);
    Task<BuddyGrant>       CreateBuddyGrantAsync(BuddyGrant grant);
    Task<bool>             UpdateBuddyGrantAsync(BuddyGrant grant);
    Task<bool>             RevokeBuddyGrantAsync(Guid grantId, Guid grantorId);
    Task<bool>             HasPermissionAsync(Guid granteeId, Guid grantorId, string permission);
    Task<List<string>>     GetPermissionsForUserAsync(Guid granteeId);  // union of all received grants
}
