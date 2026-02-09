using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.HR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Hr_CustomFields_SystemSeed_v2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'hr'
          AND table_name = 'hr_custom_fields'
    ) THEN
        ALTER TABLE hr.hr_custom_fields
            ALTER COLUMN is_system SET DEFAULT false;
    END IF;
END $$;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'hr'
          AND table_name = 'hr_custom_fields'
    ) THEN
        ALTER TABLE hr.hr_custom_fields
            ALTER COLUMN is_system DROP DEFAULT;
    END IF;
END $$;");
        }
    }
}
