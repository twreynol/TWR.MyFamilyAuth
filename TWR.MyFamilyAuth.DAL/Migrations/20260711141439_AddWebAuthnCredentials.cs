using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TWR.MyFamilyAuth.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddWebAuthnCredentials : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WebAuthnChallenges",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FamilyUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAppId = table.Column<Guid>(type: "uuid", nullable: false),
                    RpId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ChallengeToken = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ChallengeKind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OptionsJson = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebAuthnChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebAuthnChallenges_FamilyUsers_FamilyUserId",
                        column: x => x.FamilyUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WebAuthnChallenges_RegisteredApps_RegisteredAppId",
                        column: x => x.RegisteredAppId,
                        principalTable: "RegisteredApps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebAuthnCredentials",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FamilyUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAppId = table.Column<Guid>(type: "uuid", nullable: false),
                    RpId = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CredentialId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    PublicKey = table.Column<string>(type: "text", nullable: false),
                    SignCount = table.Column<long>(type: "bigint", nullable: false),
                    UserHandle = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    AaGuid = table.Column<Guid>(type: "uuid", nullable: true),
                    Transports = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeviceLabel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebAuthnCredentials", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebAuthnCredentials_FamilyUsers_FamilyUserId",
                        column: x => x.FamilyUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WebAuthnCredentials_RegisteredApps_RegisteredAppId",
                        column: x => x.RegisteredAppId,
                        principalTable: "RegisteredApps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WebAuthnChallenges_ChallengeToken",
                table: "WebAuthnChallenges",
                column: "ChallengeToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebAuthnChallenges_FamilyUserId",
                table: "WebAuthnChallenges",
                column: "FamilyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WebAuthnChallenges_RegisteredAppId",
                table: "WebAuthnChallenges",
                column: "RegisteredAppId");

            migrationBuilder.CreateIndex(
                name: "IX_WebAuthnCredentials_CredentialId",
                table: "WebAuthnCredentials",
                column: "CredentialId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WebAuthnCredentials_FamilyUserId",
                table: "WebAuthnCredentials",
                column: "FamilyUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WebAuthnCredentials_RegisteredAppId",
                table: "WebAuthnCredentials",
                column: "RegisteredAppId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WebAuthnChallenges");

            migrationBuilder.DropTable(
                name: "WebAuthnCredentials");
        }
    }
}
