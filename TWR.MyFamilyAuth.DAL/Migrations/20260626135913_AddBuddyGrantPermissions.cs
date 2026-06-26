using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TWR.MyFamilyAuth.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddBuddyGrantPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BuddyGrants_GrantorId",
                table: "BuddyGrants");

            migrationBuilder.AlterColumn<DateTime>(
                name: "GrantedAt",
                table: "BuddyGrants",
                type: "timestamp with time zone",
                nullable: false,
                defaultValueSql: "now()",
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone");

            migrationBuilder.AddColumn<string[]>(
                name: "Permissions",
                table: "BuddyGrants",
                type: "text[]",
                nullable: false,
                defaultValue: new string[0]);

            migrationBuilder.CreateIndex(
                name: "IX_BuddyGrants_GrantorId_GranteeId",
                table: "BuddyGrants",
                columns: new[] { "GrantorId", "GranteeId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_BuddyGrants_NoSelfGrant",
                table: "BuddyGrants",
                sql: "\"GrantorId\" <> \"GranteeId\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BuddyGrants_GrantorId_GranteeId",
                table: "BuddyGrants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BuddyGrants_NoSelfGrant",
                table: "BuddyGrants");

            migrationBuilder.DropColumn(
                name: "Permissions",
                table: "BuddyGrants");

            migrationBuilder.AlterColumn<DateTime>(
                name: "GrantedAt",
                table: "BuddyGrants",
                type: "timestamp with time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp with time zone",
                oldDefaultValueSql: "now()");

            migrationBuilder.CreateIndex(
                name: "IX_BuddyGrants_GrantorId",
                table: "BuddyGrants",
                column: "GrantorId");
        }
    }
}
