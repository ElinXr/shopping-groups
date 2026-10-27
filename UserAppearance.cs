using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShoppingGroups.Api.Migrations;

[DbContext(typeof(AppDb))]
[Migration("20261026000100_UserAppearance")]
public class UserAppearance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "AspNetUsers" ADD COLUMN "Theme" varchar(10) NOT NULL DEFAULT 'light';
        ALTER TABLE "AspNetUsers" ADD CONSTRAINT "CK_Users_Theme" CHECK ("Theme" IN ('light', 'dark'));
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "AspNetUsers" DROP CONSTRAINT "CK_Users_Theme", DROP COLUMN "Theme";
        """);
}
