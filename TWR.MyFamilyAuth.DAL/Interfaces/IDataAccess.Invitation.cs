using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL.Interfaces;

public partial interface IDataAccess
{
    Task<Invitation>  CreateInvitationAsync(Invitation invitation);
    Task<Invitation?> GetInvitationByTokenAsync(string token);
    Task<bool>        AcceptInvitationAsync(string token);
}
