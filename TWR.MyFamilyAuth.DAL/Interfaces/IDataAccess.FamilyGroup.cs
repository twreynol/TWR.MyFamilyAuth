using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    Task<FamilyGroup?>       GetGroupByIdAsync(Guid id);
    Task<List<FamilyGroup>>  GetGroupsByUserAsync(Guid familyUserId);
    Task<List<FamilyGroup>>  GetAllGroupsAsync();
    Task<FamilyGroup>        CreateGroupAsync(FamilyGroup group);
    Task<FamilyGroup?>       UpdateGroupAsync(FamilyGroup group);
    Task<bool>               DeactivateGroupAsync(Guid id);
    Task<GroupMember?>       GetGroupMemberAsync(Guid familyUserId, Guid familyGroupId);
    Task<GroupMember>        AddGroupMemberAsync(GroupMember member);
    Task<bool>               RemoveGroupMemberAsync(Guid familyUserId, Guid familyGroupId);
}
