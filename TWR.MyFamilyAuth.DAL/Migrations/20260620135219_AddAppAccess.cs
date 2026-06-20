using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TWR.MyFamilyAuth.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddAppAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppAccesses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FamilyUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RegisteredAppId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppRole = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    GrantedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppAccesses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppAccesses_FamilyUsers_FamilyUserId",
                        column: x => x.FamilyUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AppAccesses_FamilyUsers_GrantedByUserId",
                        column: x => x.GrantedByUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AppAccesses_RegisteredApps_RegisteredAppId",
                        column: x => x.RegisteredAppId,
                        principalTable: "RegisteredApps",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppAccesses_FamilyUserId_RegisteredAppId",
                table: "AppAccesses",
                columns: new[] { "FamilyUserId", "RegisteredAppId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppAccesses_GrantedByUserId",
                table: "AppAccesses",
                column: "GrantedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppAccesses_RegisteredAppId",
                table: "AppAccesses",
                column: "RegisteredAppId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppAccesses");
        }
    }
}
