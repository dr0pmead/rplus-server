using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RPlus.Access.Infrastructure.Persistence;

#nullable disable

namespace RPlus.Access.Infrastructure.Migrations;

[DbContext(typeof(AccessDbContext))]
[Migration("20260126124500_AddRowVersionDefaults")]
public partial class AddRowVersionDefaults : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE access.access_policies
              ALTER COLUMN row_version SET DEFAULT decode(md5(random()::text), 'hex');

            UPDATE access.access_policies
            SET row_version = decode(md5(random()::text), 'hex')
            WHERE row_version IS NULL;

            ALTER TABLE access.sod_policy_sets
              ALTER COLUMN row_version SET DEFAULT decode(md5(random()::text), 'hex');

            UPDATE access.sod_policy_sets
            SET row_version = decode(md5(random()::text), 'hex')
            WHERE row_version IS NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            ALTER TABLE access.access_policies
              ALTER COLUMN row_version DROP DEFAULT;

            ALTER TABLE access.sod_policy_sets
              ALTER COLUMN row_version DROP DEFAULT;
            """);
    }
}
