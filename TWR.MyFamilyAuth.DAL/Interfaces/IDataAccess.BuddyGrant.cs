using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    Task<List<BuddyGrant>>  GetBuddyGrantsForUserAsync(Guid familyUserId);
    Task<BuddyGrant>        CreateBuddyGrantAsync(BuddyGrant grant);
    Task<bool>              RevokeBuddyGrantAsync(Guid grantId, Guid grantorId);
}
