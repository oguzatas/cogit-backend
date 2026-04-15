using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddScoringEngineV2_ScaleKey_ResultText : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScoringScales_TestId",
                table: "ScoringScales");

            migrationBuilder.AddColumn<int>(
                name: "CalculationOrder",
                table: "ScoringScales",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "ScoringScales",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AlterColumn<decimal>(
                name: "CalculatedScore",
                table: "AssignmentResults",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4);

            migrationBuilder.AddColumn<string>(
                name: "ResultText",
                table: "AssignmentResults",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoringScales_TestId_Key",
                table: "ScoringScales",
                columns: new[] { "TestId", "Key" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ScoringScales_TestId_Key",
                table: "ScoringScales");

            migrationBuilder.DropColumn(
                name: "CalculationOrder",
                table: "ScoringScales");

            migrationBuilder.DropColumn(
                name: "Key",
                table: "ScoringScales");

            migrationBuilder.DropColumn(
                name: "ResultText",
                table: "AssignmentResults");

            migrationBuilder.AlterColumn<decimal>(
                name: "CalculatedScore",
                table: "AssignmentResults",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ScoringScales_TestId",
                table: "ScoringScales",
                column: "TestId");
        }
    }
}
