using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TWR.MyFamilyAuth.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddTwoFactorAndDeviceTrust : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Requires2FA",
                table: "RegisteredApps",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "DeviceTrusts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FamilyUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    TokenHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    AppClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DeviceLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceTrusts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceTrusts_FamilyUsers_FamilyUserId",
                        column: x => x.FamilyUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TwoFactorChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FamilyUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAppId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChallengeToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OtpHash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TwoFactorChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TwoFactorChallenges_FamilyUsers_FamilyUserId",
                        column: x => x.FamilyUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TwoFactorChallenges_RegisteredApps_RegisteredAppId",
                        column: x => x.RegisteredAppId,
                        principalTable: "RegisteredApps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTrusts_FamilyUserId",
                table: "DeviceTrusts",
                column: "FamilyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceTrusts_TokenHash",
                table: "DeviceTrusts",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorChallenges_ChallengeToken",
                table: "TwoFactorChallenges",
                column: "ChallengeToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorChallenges_FamilyUserId",
                table: "TwoFactorChallenges",
                column: "FamilyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TwoFactorChallenges_RegisteredAppId",
                table: "TwoFactorChallenges",
                column: "RegisteredAppId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeviceTrusts");

            migrationBuilder.DropTable(
                name: "TwoFactorChallenges");

            migrationBuilder.DropColumn(
                name: "Requires2FA",
                table: "RegisteredApps");
        }
    }
}
