using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RPlus.Access.Infrastructure.Persistence;

#nullable disable

namespace RPlus.Access.Infrastructure.Migrations;

[DbContext(typeof(AccessDbContext))]
[Migration("20260125054000_FixRootRegistryAndPermissionsSchemaDrift")]
public partial class FixRootRegistryAndPermissionsSchemaDrift : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Root registry table was created earlier with quoted PascalCase columns.
        // Access runtime uses snake_case naming convention, so queries fail until we align the schema.
        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'access'
                  AND table_name = 'root_registry'
                  AND column_name = 'HashedUserId'
              ) THEN
                ALTER TABLE access.root_registry RENAME COLUMN "HashedUserId" TO hashed_user_id;
              END IF;

              IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'access'
                  AND table_name = 'root_registry'
                  AND column_name = 'CreatedAt'
              ) THEN
                ALTER TABLE access.root_registry RENAME COLUMN "CreatedAt" TO created_at;
              END IF;

              IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'access'
                  AND table_name = 'root_registry'
                  AND column_name = 'Status'
              ) THEN
                ALTER TABLE access.root_registry RENAME COLUMN "Status" TO status;
              END IF;
            END $$;
            """);

        // Permission discovery metadata: current model expects "source_service".
        migrationBuilder.Sql("""ALTER TABLE access.features ADD COLUMN IF NOT EXISTS source_service character varying(100);""");
        migrationBuilder.Sql("""CREATE INDEX IF NOT EXISTS "IX_features_source_service" ON access.features (source_service);""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP INDEX IF EXISTS access."IX_features_source_service";""");
        migrationBuilder.Sql("""ALTER TABLE access.features DROP COLUMN IF EXISTS source_service;""");

        migrationBuilder.Sql(
            """
            DO $$
            BEGIN
              IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'access'
                  AND table_name = 'root_registry'
                  AND column_name = 'hashed_user_id'
              ) THEN
                ALTER TABLE access.root_registry RENAME COLUMN hashed_user_id TO "HashedUserId";
              END IF;

              IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'access'
                  AND table_name = 'root_registry'
                  AND column_name = 'created_at'
              ) THEN
                ALTER TABLE access.root_registry RENAME COLUMN created_at TO "CreatedAt";
              END IF;

              IF EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'access'
                  AND table_name = 'root_registry'
                  AND column_name = 'status'
              ) THEN
                ALTER TABLE access.root_registry RENAME COLUMN status TO "Status";
              END IF;
            END $$;
            """);
    }
}
