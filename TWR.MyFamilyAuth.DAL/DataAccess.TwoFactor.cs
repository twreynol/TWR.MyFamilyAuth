using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess
{
    public async Task<TwoFactorChallenge> CreateTwoFactorChallengeAsync(TwoFactorChallenge challenge)
    {
        using var db = CreateContext();
        try
        {
            challenge.CreatedAt = DateTime.UtcNow;
            db.TwoFactorChallenges.Add(challenge);
            await db.SaveChangesAsync();
            return challenge;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating 2FA challenge"); throw; }
    }

    public async Task<TwoFactorChallenge?> GetTwoFactorChallengeAsync(string challengeToken)
    {
        using var db = CreateContext();
        try
        {
            return await db.TwoFactorChallenges
                .Include(c => c.User)
                .Include(c => c.App)
                .FirstOrDefaultAsync(c =>
                    c.ChallengeToken == challengeToken &&
                    !c.IsUsed &&
                    c.ExpiresAt > DateTime.UtcNow);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching 2FA challenge"); throw; }
    }

    public async Task MarkChallengeUsedAsync(Guid challengeId)
    {
        using var db = CreateContext();
        try
        {
            var c = await db.TwoFactorChallenges.FindAsync(challengeId);
            if (c is null) return;
            c.IsUsed = true;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error marking challenge used {Id}", challengeId); throw; }
    }

    public async Task<DeviceTrust> CreateDeviceTrustAsync(DeviceTrust trust)
    {
        using var db = CreateContext();
        try
        {
            trust.CreatedAt  = DateTime.UtcNow;
            trust.LastUsedAt = DateTime.UtcNow;
            db.DeviceTrusts.Add(trust);
            await db.SaveChangesAsync();
            return trust;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating device trust"); throw; }
    }

    public async Task<DeviceTrust?> GetDeviceTrustByHashAsync(string tokenHash)
    {
        using var db = CreateContext();
        try
        {
            return await db.DeviceTrusts
                .Include(t => t.User)
                .FirstOrDefaultAsync(t =>
                    t.TokenHash == tokenHash &&
                    t.ExpiresAt > DateTime.UtcNow);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching device trust"); throw; }
    }

    public async Task UpdateDeviceTrustLastUsedAsync(Guid trustId)
    {
        using var db = CreateContext();
        try
        {
            var t = await db.DeviceTrusts.FindAsync(trustId);
            if (t is null) return;
            t.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error updating device trust last-used {Id}", trustId); throw; }
    }

    public async Task RevokeDeviceTrustAsync(Guid trustId)
    {
        using var db = CreateContext();
        try
        {
            var t = await db.DeviceTrusts.FindAsync(trustId);
            if (t is null) return;
            t.ExpiresAt = DateTime.UtcNow.AddSeconds(-1); // expire immediately
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error revoking device trust {Id}", trustId); throw; }
    }

    public async Task<List<DeviceTrust>> GetDeviceTrustsByUserAsync(Guid familyUserId)
    {
        using var db = CreateContext();
        try
        {
            return await db.DeviceTrusts
                .Where(t => t.FamilyUserId == familyUserId && t.ExpiresAt > DateTime.UtcNow)
                .OrderByDescending(t => t.LastUsedAt)
                .ToListAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error listing device trusts for {Id}", familyUserId); throw; }
    }
}
