using Microsoft.EntityFrameworkCore.Migrations;

namespace Torque.Migrations
{
    public partial class _2 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "screwdriver_CMK",
                columns: new[] { "screwdriver", "XYNJ" },
                values: new object[] { "03308K9290100000411307010-B720/B*000", "720.8" });

            migrationBuilder.InsertData(
                table: "screwdriver_CMK",
                columns: new[] { "screwdriver", "XYNJ" },
                values: new object[] { "03308L9080100001181307010-C289*000", "289" });

            migrationBuilder.InsertData(
                table: "screwdriver_CMK",
                columns: new[] { "screwdriver", "XYNJ" },
                values: new object[] { "720289", "0.13" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "screwdriver_CMK",
                keyColumn: "screwdriver",
                keyValue: "03308K9290100000411307010-B720/B*000");

            migrationBuilder.DeleteData(
                table: "screwdriver_CMK",
                keyColumn: "screwdriver",
                keyValue: "03308L9080100001181307010-C289*000");

            migrationBuilder.DeleteData(
                table: "screwdriver_CMK",
                keyColumn: "screwdriver",
                keyValue: "720289");
        }
    }
}
