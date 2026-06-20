using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public partial class DataAccess
{
    public async Task<FamilyUser?> GetUserByIdAsync(Guid id)
    {
        using var db = CreateContext();
        try { return await db.FamilyUsers.Include(u => u.PrimaryGroup).FirstOrDefaultAsync(u => u.Id == id); }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching user {Id}", id); throw; }
    }

    public async Task<FamilyUser?> GetUserByEmailAsync(string email)
    {
        using var db = CreateContext();
        try { return await db.FamilyUsers.FirstOrDefaultAsync(u => u.Email == email); }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching user by email {Email}", email); throw; }
    }

    public async Task<List<FamilyUser>> GetAllUsersAsync(int page, int pageSize, string? search)
    {
        using var db = CreateContext();
        try
        {
            var q = db.FamilyUsers.Include(u => u.PrimaryGroup).AsQueryable();
            if (!string.IsNullOrEmpty(search))
                q = q.Where(u => u.Email.Contains(search) || u.FirstName.Contains(search) || u.LastName.Contains(search));
            return await q.OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
                          .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error listing users"); throw; }
    }

    public async Task<int> GetUserCountAsync(string? search)
    {
        using var db = CreateContext();
        try
        {
            var q = db.FamilyUsers.AsQueryable();
            if (!string.IsNullOrEmpty(search))
                q = q.Where(u => u.Email.Contains(search) || u.FirstName.Contains(search) || u.LastName.Contains(search));
            return await q.CountAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error counting users"); throw; }
    }

    public async Task<FamilyUser> CreateUserAsync(FamilyUser user)
    {
        using var db = CreateContext();
        try
        {
            user.CreatedAt = DateTime.UtcNow;
            db.FamilyUsers.Add(user);
            await db.SaveChangesAsync();
            return user;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating user {Email}", user.Email); throw; }
    }

    public async Task<FamilyUser?> UpdateUserAsync(FamilyUser user)
    {
        using var db = CreateContext();
        try
        {
            var existing = await db.FamilyUsers.FindAsync(user.Id);
            if (existing is null) return null;
            existing.FirstName           = user.FirstName;
            existing.LastName            = user.LastName;
            existing.Email               = user.Email;
            existing.Role                = user.Role;
            existing.IsActive            = user.IsActive;
            existing.IsWard              = user.IsWard;
            existing.GuardianId          = user.GuardianId;
            existing.MustChangePassword  = user.MustChangePassword;
            existing.PasswordChangeLocked = user.PasswordChangeLocked;
            existing.AvatarBase64        = user.AvatarBase64;
            existing.TimeZoneId          = user.TimeZoneId;
            existing.PrimaryGroupId      = user.PrimaryGroupId;
            existing.UpdatedAt           = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return existing;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error updating user {Id}", user.Id); throw; }
    }

    public async Task UpdatePasswordAsync(Guid userId, string passwordHash)
    {
        using var db = CreateContext();
        try
        {
            var user = await db.FamilyUsers.FindAsync(userId);
            if (user is null) return;
            user.PasswordHash        = passwordHash;
            user.MustChangePassword  = false;
            user.UpdatedAt           = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error updating password for {Id}", userId); throw; }
    }

    public async Task UpdateLastAccessedAsync(Guid userId)
    {
        using var db = CreateContext();
        try
        {
            var user = await db.FamilyUsers.FindAsync(userId);
            if (user is null) return;
            user.LastAccessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        catch (Exception ex) { _logger.LogError(ex, "Error updating last accessed for {Id}", userId); throw; }
    }

    public async Task<bool> DeactivateUserAsync(Guid id)
    {
        using var db = CreateContext();
        try
        {
            var user = await db.FamilyUsers.FindAsync(id);
            if (user is null) return false;
            user.IsActive  = false;
            user.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error deactivating user {Id}", id); throw; }
    }

    public async Task<FamilyUser> SeedSuperAdminAsync(string email, string firstName, string lastName, string passwordHash)
    {
        using var db = CreateContext();
        try
        {
            var existing = await db.FamilyUsers.FirstOrDefaultAsync(u => u.Email == email);
            if (existing is not null) return existing;
            var admin = new FamilyUser
            {
                FirstName    = firstName,
                LastName     = lastName,
                Email        = email,
                PasswordHash = passwordHash,
                Role         = Entities.FamilyRoles.SuperAdmin,
                IsActive     = true,
                CreatedAt    = DateTime.UtcNow
            };
            db.FamilyUsers.Add(admin);
            await db.SaveChangesAsync();
            return admin;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error seeding super admin {Email}", email); throw; }
    }
}
