using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess
{
    public async Task<Invitation> CreateInvitationAsync(Invitation invitation)
    {
        using var db = CreateContext();
        try
        {
            invitation.CreatedAt = DateTime.UtcNow;
            db.Invitations.Add(invitation);
            await db.SaveChangesAsync();
            return invitation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating invitation for {Email}", invitation.InviteeEmail);
            throw;
        }
    }

    public async Task<Invitation?> GetInvitationByTokenAsync(string token)
    {
        using var db = CreateContext();
        try
        {
            return await db.Invitations
                .Include(i => i.InvitedBy)
                .FirstOrDefaultAsync(i => i.Token == token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting invitation by token");
            throw;
        }
    }

    public async Task<bool> AcceptInvitationAsync(string token)
    {
        using var db = CreateContext();
        try
        {
            var invitation = await db.Invitations
                .FirstOrDefaultAsync(i => i.Token == token && !i.IsAccepted);
            if (invitation is null || invitation.ExpiresAt < DateTime.UtcNow) return false;

            invitation.IsAccepted = true;
            invitation.AcceptedAt = DateTime.UtcNow;
            return await db.SaveChangesAsync() > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting invitation by token");
            throw;
        }
    }
}
