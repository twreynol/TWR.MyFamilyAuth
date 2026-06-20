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
            e.Property(x => x.Token).IsRequired().HasMaxLength(100);
            e.HasIndex(x => x.Token).IsUnique();
            e.HasOne(x => x.Group).WithMany()
             .HasForeignKey(x => x.FamilyGroupId).OnDelete(DeleteBehavior.Cascade);
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
    }
}
