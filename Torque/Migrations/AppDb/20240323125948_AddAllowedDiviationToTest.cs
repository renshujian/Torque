using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torque.Migrations.AppDb
{
    public partial class AddAllowedDiviationToTest : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AllowedDiviation",
                table: "Tests",
                type: "REAL",
                nullable: false,
                defaultValue: 0.2);

            migrationBuilder.CreateIndex(
                name: "IX_Tests_ToolId",
                table: "Tests",
                column: "ToolId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tests_ToolId",
                table: "Tests");

            migrationBuilder.DropColumn(
                name: "AllowedDiviation",
                table: "Tests");
        }
    }
}
