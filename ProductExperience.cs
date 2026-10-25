using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShoppingGroups.Api.Migrations;

[DbContext(typeof(AppDb))]
[Migration("20261025000300_ProductExperience")]
public class ProductExperience : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "Products" ADD COLUMN "Quantity" numeric(9,3) NOT NULL DEFAULT 1;
        ALTER TABLE "Products" ADD COLUMN "Unit" varchar(10) NOT NULL DEFAULT 'pcs';
        ALTER TABLE "Products" ADD CONSTRAINT "CK_Products_Quantity" CHECK ("Quantity" > 0 AND "Quantity" <= 999999.999);
        ALTER TABLE "Products" ADD CONSTRAINT "CK_Products_Unit" CHECK ("Unit" IN ('pcs', 'kg', 'g', 'l', 'ml', 'pack'));
        ALTER TABLE "Notifications" ADD COLUMN "Title" text NOT NULL DEFAULT '';
        ALTER TABLE "Notifications" ADD COLUMN "SenderName" text NOT NULL DEFAULT '';
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "Notifications" DROP COLUMN "SenderName", DROP COLUMN "Title";
        ALTER TABLE "Products" DROP CONSTRAINT "CK_Products_Unit", DROP CONSTRAINT "CK_Products_Quantity";
        ALTER TABLE "Products" DROP COLUMN "Unit", DROP COLUMN "Quantity";
        """);
}
