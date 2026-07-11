using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess
{
    public async Task<WebAuthnCredential> CreateWebAuthnCredentialAsync(WebAuthnCredential credential)
    {
        using var db = CreateContext();
        try
        {
            credential.CreatedAt  = DateTime.UtcNow;
            credential.LastUsedAt = DateTime.UtcNow;
            db.WebAuthnCredentials.Add(credential);
            await db.SaveChangesAsync();
            return credential;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating WebAuthn credential"); throw; }
    }

    public async Task<List<WebAuthnCredential>> GetWebAuthnCredentialsByUserAndRpIdAsync(Guid familyUserId, string rpId)
    {
        using var db = CreateContext();
        try
        {
            return await db.WebAuthnCredentials
                .Where(c => c.FamilyUserId == familyUserId && c.RpId == rpId)
                .ToListAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error listing WebAuthn credentials for {Id}/{RpId}", familyUserId, rpId); throw; }
    }

    public async Task<WebAuthnCredential?> GetWebAuthnCredentialByCredentialIdAsync(string credentialId)
    {
        using var db = CreateContext();
        try
        {
            return await db.WebAuthnCredentials
                .Include(c => c.User)
                .Include(c => c.App)
                .FirstOrDefaultAsync(c => c.CredentialId == credentialId);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching WebAuthn credential"); throw; }
    }

    public async Task UpdateWebAuthnCredentialSignCountAsync(Guid credentialRecordId, long signCount)
    {
        using var db = CreateContext();
        try
        {
            var c = await db.WebAuthnCredentials.FindAsync(credentialRecordId);
            if (c is null) return;
            c.SignCount  = signCount;
            c.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error updating WebAuthn sign count {Id}", credentialRecordId); throw; }
    }

    public async Task<bool> DeleteWebAuthnCredentialAsync(Guid credentialRecordId, Guid familyUserId)
    {
        using var db = CreateContext();
        try
        {
            var c = await db.WebAuthnCredentials.FindAsync(credentialRecordId);
            if (c is null || c.FamilyUserId != familyUserId) return false;
            db.WebAuthnCredentials.Remove(c);
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error deleting WebAuthn credential {Id}", credentialRecordId); throw; }
    }

    public async Task<WebAuthnChallenge> CreateWebAuthnChallengeAsync(WebAuthnChallenge challenge)
    {
        using var db = CreateContext();
        try
        {
            challenge.CreatedAt = DateTime.UtcNow;
            db.WebAuthnChallenges.Add(challenge);
            await db.SaveChangesAsync();
            return challenge;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating WebAuthn challenge"); throw; }
    }

    public async Task<WebAuthnChallenge?> GetWebAuthnChallengeAsync(string challengeToken)
    {
        using var db = CreateContext();
        try
        {
            return await db.WebAuthnChallenges
                .Include(c => c.User)
                .Include(c => c.App)
                .FirstOrDefaultAsync(c =>
                    c.ChallengeToken == challengeToken &&
                    !c.IsUsed &&
                    c.ExpiresAt > DateTime.UtcNow);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching WebAuthn challenge"); throw; }
    }

    public async Task MarkWebAuthnChallengeUsedAsync(Guid challengeId)
    {
        using var db = CreateContext();
        try
        {
            var c = await db.WebAuthnChallenges.FindAsync(challengeId);
            if (c is null) return;
            c.IsUsed = true;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error marking WebAuthn challenge used {Id}", challengeId); throw; }
    }
}
