using Microsoft.EntityFrameworkCore.Migrations;

namespace Torque.Migrations
{
    public partial class _3 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "screwdriver_CMK",
                columns: new[] { "screwdriver", "XYNJ" },
                values: new object[] { "50mppmu1N0vovmnmmmmmqnnpmtmnmj1E0toml1E0gmmm", "50" });

            migrationBuilder.InsertData(
                table: "screwdriver_CMK",
                columns: new[] { "screwdriver", "XYNJ" },
                values: new object[] { "90ehhem1G0nemefeeeeffmfhelefebxgmnYeee", "9" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "screwdriver_CMK",
                keyColumn: "screwdriver",
                keyValue: "50mppmu1N0vovmnmmmmmqnnpmtmnmj1E0toml1E0gmmm");

            migrationBuilder.DeleteData(
                table: "screwdriver_CMK",
                keyColumn: "screwdriver",
                keyValue: "90ehhem1G0nemefeeeeffmfhelefebxgmnYeee");
        }
    }
}
