using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Torque.Migrations
{
    public partial class _1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "real_torque_of_screwdriver",
                columns: table => new
                {
                    test_time = table.Column<DateTime>(type: "TIMESTAMP(7)", nullable: false),
                    screwdriver = table.Column<string>(type: "NVARCHAR2(2000)", nullable: false),
                    set_torque = table.Column<string>(type: "NVARCHAR2(64)", nullable: false),
                    real_torque = table.Column<string>(type: "NVARCHAR2(64)", nullable: false),
                    diviation = table.Column<string>(type: "NVARCHAR2(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_real_torque_of_screwdriver", x => x.test_time);
                });

            migrationBuilder.CreateTable(
                name: "screwdriver_CMK",
                columns: table => new
                {
                    screwdriver = table.Column<string>(type: "NVARCHAR2(450)", nullable: false),
                    XYNJ = table.Column<string>(type: "NVARCHAR2(64)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_screwdriver_CMK", x => x.screwdriver);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "real_torque_of_screwdriver");

            migrationBuilder.DropTable(
                name: "screwdriver_CMK");
        }
    }
}
