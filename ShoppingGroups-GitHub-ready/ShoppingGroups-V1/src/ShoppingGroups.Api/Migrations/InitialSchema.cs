using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ShoppingGroups.Api.Migrations;

[DbContext(typeof(AppDb))]
[Migration("20261022000100_InitialSchema")]
public class InitialSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        using var stream = typeof(InitialSchema).Assembly.GetManifestResourceStream("ShoppingGroups.Api.Migrations.InitialSchema.sql")!;
        using var reader = new StreamReader(stream);
        migrationBuilder.Sql(reader.ReadToEnd());
    }

    protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.Sql("""
        DROP TABLE "AdminAudits", "Emails", "Notifications", "Activities", "Invitations", "Archives", "Sessions", "Products", "Lists", "Members", "Groups",
          "AspNetUserTokens", "AspNetUserLogins", "AspNetUserClaims", "AspNetUserRoles", "AspNetRoleClaims", "AspNetRoles", "AspNetUsers";
        """);
}
