using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShoppingGroups.Api.Migrations;

[DbContext(typeof(AppDb))]
[Migration("20261025000200_DetailedDevicePush")]
public class DetailedDevicePush : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "PushDevices" ADD COLUMN "P256dh" text NOT NULL DEFAULT '';
        ALTER TABLE "PushDevices" ADD COLUMN "Auth" text NOT NULL DEFAULT '';
        """);
    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        ALTER TABLE "PushDevices" DROP COLUMN "P256dh";
        ALTER TABLE "PushDevices" DROP COLUMN "Auth";
        """);
}