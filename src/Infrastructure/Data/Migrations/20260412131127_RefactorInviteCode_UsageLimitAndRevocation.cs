using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class RefactorInviteCode_UsageLimitAndRevocation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "IsUsed",
                table: "InviteCodes",
                newName: "IsRevoked");

            migrationBuilder.AddColumn<int>(
                name: "MaxUses",
                table: "InviteCodes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "RevokedAt",
                table: "InviteCodes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UsageCount",
                table: "InviteCodes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxUses",
                table: "InviteCodes");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "InviteCodes");

            migrationBuilder.DropColumn(
                name: "UsageCount",
                table: "InviteCodes");

            migrationBuilder.RenameColumn(
                name: "IsRevoked",
                table: "InviteCodes",
                newName: "IsUsed");
        }
    }
}
