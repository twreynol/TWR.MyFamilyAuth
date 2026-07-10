using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TWR.MyFamilyAuth.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddUserSetting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    FamilyUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AppClientId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SettingKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    SettingValue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UpdatedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSettings_FamilyUsers_FamilyUserId",
                        column: x => x.FamilyUserId,
                        principalTable: "FamilyUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserSettings_FamilyUserId_AppClientId_SettingKey",
                table: "UserSettings",
                columns: new[] { "FamilyUserId", "AppClientId", "SettingKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSettings");
        }
    }
}
