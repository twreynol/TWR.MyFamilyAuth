using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TWR.MyFamilyAuth.DAL.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FamilyUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AppClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FamilyGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ParentGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyGroups_FamilyGroups_ParentGroupId",
                        column: x => x.ParentGroupId,
                        principalTable: "FamilyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RegisteredApps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ClientSecretHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AllowedOrigins = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RegisteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegisteredApps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FamilyUsers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsWard = table.Column<bool>(type: "boolean", nullable: false),
                    GuardianId = table.Column<Guid>(type: "uuid", nullable: true),
                    MustChangePassword = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordChangeLocked = table.Column<bool>(type: "boolean", nullable: false),
                    AvatarBase64 = table.Column<string>(type: "text", nullable: true),
                    TimeZoneId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PrimaryGroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastAccessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FamilyUsers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FamilyUsers_FamilyGroups_PrimaryGroupId",
                        column: x => x.PrimaryGroupId,
                        principalTable: "FamilyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_FamilyUsers_FamilyUsers_GuardianId",
                        column: x => x.GuardianId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BuddyGrants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    GrantorId = table.Column<Guid>(type: "uuid", nullable: false),
                    GranteeId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BuddyGrants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BuddyGrants_FamilyUsers_GranteeId",
                        column: x => x.GranteeId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BuddyGrants_FamilyUsers_GrantorId",
                        column: x => x.GrantorId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "GroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FamilyUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FamilyGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsLimitedMember = table.Column<bool>(type: "boolean", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GroupMembers_FamilyGroups_FamilyGroupId",
                        column: x => x.FamilyGroupId,
                        principalTable: "FamilyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupMembers_FamilyUsers_FamilyUserId",
                        column: x => x.FamilyUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Invitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FamilyGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    InviteeEmail = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    Token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    InvitedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsAccepted = table.Column<bool>(type: "boolean", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Invitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Invitations_FamilyGroups_FamilyGroupId",
                        column: x => x.FamilyGroupId,
                        principalTable: "FamilyGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Invitations_FamilyUsers_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FamilyUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Token = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_FamilyUsers_FamilyUserId",
                        column: x => x.FamilyUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FamilyUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AppClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRevoked = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_FamilyUsers_FamilyUserId",
                        column: x => x.FamilyUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BuddyGrants_GranteeId",
                table: "BuddyGrants",
                column: "GranteeId");

            migrationBuilder.CreateIndex(
                name: "IX_BuddyGrants_GrantorId",
                table: "BuddyGrants",
                column: "GrantorId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyGroups_ParentGroupId",
                table: "FamilyGroups",
                column: "ParentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyUsers_Email",
                table: "FamilyUsers",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FamilyUsers_GuardianId",
                table: "FamilyUsers",
                column: "GuardianId");

            migrationBuilder.CreateIndex(
                name: "IX_FamilyUsers_PrimaryGroupId",
                table: "FamilyUsers",
                column: "PrimaryGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_FamilyGroupId",
                table: "GroupMembers",
                column: "FamilyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_FamilyUserId_FamilyGroupId",
                table: "GroupMembers",
                columns: new[] { "FamilyUserId", "FamilyGroupId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_FamilyGroupId",
                table: "Invitations",
                column: "FamilyGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_InvitedByUserId",
                table: "Invitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Token",
                table: "Invitations",
                column: "Token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_FamilyUserId",
                table: "PasswordResetTokens",
                column: "FamilyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyUserId",
                table: "RefreshTokens",
                column: "FamilyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RegisteredApps_ClientId",
                table: "RegisteredApps",
                column: "ClientId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropTable(
                name: "BuddyGrants");

            migrationBuilder.DropTable(
                name: "GroupMembers");

            migrationBuilder.DropTable(
                name: "Invitations");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "RefreshTokens");

            migrationBuilder.DropTable(
                name: "RegisteredApps");

            migrationBuilder.DropTable(
                name: "FamilyUsers");

            migrationBuilder.DropTable(
                name: "FamilyGroups");
        }
    }
}
