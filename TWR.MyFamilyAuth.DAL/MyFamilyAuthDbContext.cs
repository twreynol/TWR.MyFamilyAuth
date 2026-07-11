using Microsoft.EntityFrameworkCore;
using TWR.MyFamilyAuth.DAL.Entities;

namespace TWR.MyFamilyAuth.DAL;

public class MyFamilyAuthDbContext : DbContext
{
    public MyFamilyAuthDbContext(DbContextOptions<MyFamilyAuthDbContext> options) : base(options) { }

    public DbSet<FamilyUser>         FamilyUsers         => Set<FamilyUser>();
    public DbSet<FamilyGroup>        FamilyGroups        => Set<FamilyGroup>();
    public DbSet<GroupMember>        GroupMembers        => Set<GroupMember>();
    public DbSet<BuddyGrant>         BuddyGrants         => Set<BuddyGrant>();
    public DbSet<Invitation>         Invitations         => Set<Invitation>();
    public DbSet<RegisteredApp>      RegisteredApps      => Set<RegisteredApp>();
    public DbSet<RefreshToken>       RefreshTokens       => Set<RefreshToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<AuditLog>           AuditLogs           => Set<AuditLog>();
    public DbSet<AppAccess>          AppAccesses         => Set<AppAccess>();
    public DbSet<TwoFactorChallenge> TwoFactorChallenges => Set<TwoFactorChallenge>();
    public DbSet<DeviceTrust>        DeviceTrusts        => Set<DeviceTrust>();
    public DbSet<UserAccessCache>    UserAccessCaches    => Set<UserAccessCache>();
    public DbSet<UserSetting>        UserSettings        => Set<UserSetting>();
    public DbSet<WebAuthnCredential> WebAuthnCredentials => Set<WebAuthnCredential>();
    public DbSet<WebAuthnChallenge>  WebAuthnChallenges  => Set<WebAuthnChallenge>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FamilyUser>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.FirstName).IsRequired().HasMaxLength(100);
            e.Property(x => x.LastName).IsRequired().HasMaxLength(100);
            e.Property(x => x.Email).IsRequired().HasMaxLength(320);
            e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            e.Property(x => x.Role).IsRequired().HasMaxLength(50);
            e.Property(x => x.TimeZoneId).HasMaxLength(100);
            e.Ignore(x => x.FullName);
            e.HasIndex(x => x.Email).IsUnique();
            e.HasOne(x => x.Guardian).WithMany()
             .HasForeignKey(x => x.GuardianId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.PrimaryGroup).WithMany(g => g.PrimaryUsers)
             .HasForeignKey(x => x.PrimaryGroupId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<FamilyGroup>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Name).IsRequired().HasMaxLength(200);
            e.HasOne(x => x.ParentGroup).WithMany(g => g.SubGroups)
             .HasForeignKey(x => x.ParentGroupId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GroupMember>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.GroupRole).IsRequired().HasMaxLength(50);
            e.HasIndex(x => new { x.FamilyUserId, x.FamilyGroupId }).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.GroupMemberships)
             .HasForeignKey(x => x.FamilyUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Group).WithMany(g => g.Members)
             .HasForeignKey(x => x.FamilyGroupId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BuddyGrant>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Permissions).HasColumnType("text[]").IsRequired();
            e.Property(x => x.GrantedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.GrantorId, x.GranteeId }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("CK_BuddyGrants_NoSelfGrant", "\"GrantorId\" <> \"GranteeId\""));
            e.HasOne(x => x.Grantor).WithMany(u => u.BuddyGrantsGiven)
             .HasForeignKey(x => x.GrantorId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Grantee).WithMany(u => u.BuddyGrantsReceived)
             .HasForeignKey(x => x.GranteeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Invitation>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.InviteeEmail).IsRequired().HasMaxLength(320);
            e.Property(x => x.DisplayName).HasMaxLength(200);
            e.Property(x => x.Token).IsRequired().HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
            e.HasIndex(x => x.Token).IsUnique();
            e.HasOne(x => x.Group).WithMany()
             .HasForeignKey(x => x.FamilyGroupId).OnDelete(DeleteBehavior.Cascade).IsRequired(false);
            e.HasOne(x => x.InvitedBy).WithMany()
             .HasForeignKey(x => x.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RegisteredApp>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Name).IsRequired().HasMaxLength(100);
            e.Property(x => x.ClientId).IsRequired().HasMaxLength(100);
            e.Property(x => x.ClientSecretHash).IsRequired().HasMaxLength(200);
            e.Property(x => x.SupportedRoles).HasMaxLength(500);
            e.HasIndex(x => x.ClientId).IsUnique();
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(200);
            e.Property(x => x.AppClientId).HasMaxLength(100);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.RefreshTokens)
             .HasForeignKey(x => x.FamilyUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PasswordResetToken>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Token).IsRequired().HasMaxLength(100);
            e.HasOne(x => x.User).WithMany()
             .HasForeignKey(x => x.FamilyUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppAccess>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.AppRole).HasMaxLength(50);
            e.HasIndex(x => new { x.FamilyUserId, x.RegisteredAppId }).IsUnique();
            e.HasOne(x => x.User).WithMany()
             .HasForeignKey(x => x.FamilyUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.App).WithMany()
             .HasForeignKey(x => x.RegisteredAppId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.GrantedBy).WithMany()
             .HasForeignKey(x => x.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TwoFactorChallenge>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.ChallengeToken).IsRequired().HasMaxLength(100);
            e.Property(x => x.OtpHash).IsRequired().HasMaxLength(200);
            e.HasIndex(x => x.ChallengeToken).IsUnique();
            e.HasOne(x => x.User).WithMany()
             .HasForeignKey(x => x.FamilyUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.App).WithMany()
             .HasForeignKey(x => x.RegisteredAppId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DeviceTrust>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.TokenHash).IsRequired().HasMaxLength(200);
            e.Property(x => x.AppClientId).IsRequired().HasMaxLength(100);
            e.Property(x => x.DeviceLabel).HasMaxLength(200);
            e.Property(x => x.IpAddress).HasMaxLength(50);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany()
             .HasForeignKey(x => x.FamilyUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Action).IsRequired().HasMaxLength(100);
            e.Property(x => x.IpAddress).HasMaxLength(50);
            e.Property(x => x.AppClientId).HasMaxLength(100);
            e.Property(x => x.Notes).HasMaxLength(500);
        });

        modelBuilder.Entity<UserAccessCache>(e =>
        {
            e.HasKey(x => new { x.UserId, x.AppClientId });
            e.Property(x => x.AppClientId).IsRequired().HasMaxLength(50);
            e.Property(x => x.GrantorIds).HasColumnType("uuid[]").IsRequired();
            e.Property(x => x.UpdatedAt).HasDefaultValueSql("now()");
            e.HasOne<FamilyUser>().WithMany()
             .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserSetting>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.AppClientId).HasMaxLength(100);
            e.Property(x => x.SettingKey).IsRequired().HasMaxLength(100);
            e.Property(x => x.SettingValue).IsRequired().HasMaxLength(500);
            e.Property(x => x.UpdatedUtc).HasDefaultValueSql("now()");
            e.HasIndex(x => new { x.FamilyUserId, x.AppClientId, x.SettingKey }).IsUnique();
            e.HasOne(x => x.User).WithMany()
             .HasForeignKey(x => x.FamilyUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WebAuthnCredential>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.RpId).IsRequired().HasMaxLength(255);
            e.Property(x => x.CredentialId).IsRequired().HasMaxLength(500);
            e.Property(x => x.PublicKey).IsRequired();
            e.Property(x => x.UserHandle).IsRequired().HasMaxLength(100);
            e.Property(x => x.Transports).HasMaxLength(200);
            e.Property(x => x.DeviceLabel).HasMaxLength(200);
            e.HasIndex(x => x.CredentialId).IsUnique();
            e.HasOne(x => x.User).WithMany(u => u.WebAuthnCredentials)
             .HasForeignKey(x => x.FamilyUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.App).WithMany()
             .HasForeignKey(x => x.RegisteredAppId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WebAuthnChallenge>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.RpId).IsRequired().HasMaxLength(255);
            e.Property(x => x.ChallengeToken).IsRequired().HasMaxLength(100);
            e.Property(x => x.ChallengeKind).IsRequired().HasMaxLength(20);
            e.Property(x => x.OptionsJson).IsRequired().HasMaxLength(4000);
            e.HasIndex(x => x.ChallengeToken).IsUnique();
            e.HasOne(x => x.User).WithMany()
             .HasForeignKey(x => x.FamilyUserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.App).WithMany()
             .HasForeignKey(x => x.RegisteredAppId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
