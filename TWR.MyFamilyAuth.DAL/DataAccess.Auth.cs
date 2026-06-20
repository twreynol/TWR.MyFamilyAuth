using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess
{
    public async Task<RefreshToken> StoreRefreshTokenAsync(RefreshToken token)
    {
        using var db = CreateContext();
        try { token.CreatedAt = DateTime.UtcNow; db.RefreshTokens.Add(token); await db.SaveChangesAsync(); return token; }
        catch (Exception ex) { _logger.LogError(ex, "Error storing refresh token"); throw; }
    }

    public async Task<RefreshToken?> GetRefreshTokenByHashAsync(string tokenHash)
    {
        using var db = CreateContext();
        try { return await db.RefreshTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.TokenHash == tokenHash && !t.IsRevoked); }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching refresh token"); throw; }
    }

    public async Task RevokeRefreshTokenAsync(Guid tokenId)
    {
        using var db = CreateContext();
        try
        {
            var t = await db.RefreshTokens.FindAsync(tokenId);
            if (t is null) return;
            t.IsRevoked = true; await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error revoking refresh token {Id}", tokenId); throw; }
    }

    public async Task RevokeAllRefreshTokensAsync(Guid familyUserId)
    {
        using var db = CreateContext();
        try
        {
            await db.RefreshTokens.Where(t => t.FamilyUserId == familyUserId && !t.IsRevoked)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.IsRevoked, true));
        }
        catch (Exception ex) { _logger.LogError(ex, "Error revoking all tokens for {Id}", familyUserId); throw; }
    }

    public async Task<PasswordResetToken> CreatePasswordResetTokenAsync(PasswordResetToken token)
    {
        using var db = CreateContext();
        try { token.CreatedAt = DateTime.UtcNow; db.PasswordResetTokens.Add(token); await db.SaveChangesAsync(); return token; }
        catch (Exception ex) { _logger.LogError(ex, "Error creating reset token"); throw; }
    }

    public async Task<PasswordResetToken?> GetPasswordResetTokenAsync(string token)
    {
        using var db = CreateContext();
        try { return await db.PasswordResetTokens.Include(t => t.User).FirstOrDefaultAsync(t => t.Token == token && !t.IsUsed && t.ExpiresAt > DateTime.UtcNow); }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching reset token"); throw; }
    }

    public async Task MarkPasswordResetTokenUsedAsync(Guid tokenId)
    {
        using var db = CreateContext();
        try
        {
            var t = await db.PasswordResetTokens.FindAsync(tokenId);
            if (t is null) return;
            t.IsUsed = true; await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error marking reset token used {Id}", tokenId); throw; }
    }

    public async Task WriteAuditLogAsync(AuditLog entry)
    {
        using var db = CreateContext();
        try { entry.Timestamp = DateTime.UtcNow; db.AuditLogs.Add(entry); await db.SaveChangesAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Error writing audit log"); throw; }
    }

    public async Task<RegisteredApp?> GetRegisteredAppByClientIdAsync(string clientId)
    {
        using var db = CreateContext();
        try { return await db.RegisteredApps.FirstOrDefaultAsync(a => a.ClientId == clientId && a.IsActive); }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching app {ClientId}", clientId); throw; }
    }

    public async Task<List<RegisteredApp>> GetAllRegisteredAppsAsync()
    {
        using var db = CreateContext();
        try { return await db.RegisteredApps.OrderBy(a => a.Name).ToListAsync(); }
        catch (Exception ex) { _logger.LogError(ex, "Error listing apps"); throw; }
    }

    public async Task<RegisteredApp> CreateRegisteredAppAsync(RegisteredApp app)
    {
        using var db = CreateContext();
        try { app.RegisteredAt = DateTime.UtcNow; db.RegisteredApps.Add(app); await db.SaveChangesAsync(); return app; }
        catch (Exception ex) { _logger.LogError(ex, "Error creating app {Name}", app.Name); throw; }
    }

    public async Task<bool> DeactivateRegisteredAppAsync(Guid id)
    {
        using var db = CreateContext();
        try
        {
            var a = await db.RegisteredApps.FindAsync(id);
            if (a is null) return false;
            a.IsActive = false; a.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error deactivating app {Id}", id); throw; }
    }

    public async Task<RegisteredApp?> UpdateRegisteredAppAsync(Guid id, bool? isActive = null, bool? requires2Fa = null)
    {
        using var db = CreateContext();
        try
        {
            var app = await db.RegisteredApps.FindAsync(id);
            if (app is null) return null;
            if (isActive.HasValue)    app.IsActive    = isActive.Value;
            if (requires2Fa.HasValue) app.Requires2FA = requires2Fa.Value;
            app.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return app;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error updating app {Id}", id); throw; }
    }

    public async Task<List<BuddyGrant>> GetBuddyGrantsForUserAsync(Guid familyUserId)
    {
        using var db = CreateContext();
        try
        {
            return await db.BuddyGrants
                .Include(g => g.Grantor).Include(g => g.Grantee)
                .Where(g => g.GrantorId == familyUserId || g.GranteeId == familyUserId)
                .ToListAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching buddy grants for {Id}", familyUserId); throw; }
    }

    public async Task<BuddyGrant> CreateBuddyGrantAsync(BuddyGrant grant)
    {
        using var db = CreateContext();
        try { grant.GrantedAt = DateTime.UtcNow; db.BuddyGrants.Add(grant); await db.SaveChangesAsync(); return grant; }
        catch (Exception ex) { _logger.LogError(ex, "Error creating buddy grant"); throw; }
    }

    public async Task<bool> RevokeBuddyGrantAsync(Guid grantId, Guid grantorId)
    {
        using var db = CreateContext();
        try
        {
            var g = await db.BuddyGrants.FirstOrDefaultAsync(x => x.Id == grantId && x.GrantorId == grantorId);
            if (g is null) return false;
            g.IsActive = false; g.RevokedAt = DateTime.UtcNow; await db.SaveChangesAsync(); return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error revoking buddy grant {Id}", grantId); throw; }
    }
}
