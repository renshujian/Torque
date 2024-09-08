using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Torque.Migrations.AppDb
{
    public partial class AddLastLoginTimeToUser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLoginTime",
                table: "AspNetUsers",
                type: "TEXT",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "3f2b0240-bafb-4788-bc3e-e913b50b6564",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "8efb5171-3990-4070-b683-9c69a803e0f2", "3ef49ecb-b026-478a-9371-cbc2d1af8fd9" });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "8d5c1911-a3e9-4304-8893-5dae34c01121",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "22364b28-0ce1-4301-8011-c8041bbec5f9", "df942a84-92eb-4a6c-8755-8bcaae12f8ab" });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "d2b7766c-c245-4ebd-bde3-78b8a6dd134d",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "45803667-7e98-464d-af72-0de91ac40498", "082515cf-2cf6-47ce-b296-0b28247b9e9f" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLoginTime",
                table: "AspNetUsers");

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "3f2b0240-bafb-4788-bc3e-e913b50b6564",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "0552b86e-b8f9-48d9-bc61-e44fe587850d", "9d5f37c0-17c2-44df-938b-49636e063ccb" });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "8d5c1911-a3e9-4304-8893-5dae34c01121",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "094c8d03-fada-45b7-8a67-b9b65313ffc7", "658db7de-cd26-45d2-bd15-d1b4649ee2af" });

            migrationBuilder.UpdateData(
                table: "AspNetUsers",
                keyColumn: "Id",
                keyValue: "d2b7766c-c245-4ebd-bde3-78b8a6dd134d",
                columns: new[] { "ConcurrencyStamp", "SecurityStamp" },
                values: new object[] { "522ff4ae-6df5-4c9b-b097-69bef4aaa7b5", "df3e4ac0-2837-4f93-86e6-6c55b76cb5f0" });
        }
    }
}
