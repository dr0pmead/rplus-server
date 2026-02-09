using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.Users.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveFioFromUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove FIO columns from users table
            // Data has already been migrated to hr.hr_profiles
            migrationBuilder.DropColumn(
                name: "FirstName",
                schema: "users",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastName",
                schema: "users",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MiddleName",
                schema: "users",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Re-add FIO columns (for rollback purposes)
            migrationBuilder.AddColumn<string>(
                name: "FirstName",
                schema: "users",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LastName",
                schema: "users",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MiddleName",
                schema: "users",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            // Note: Data would need to be copied back from HR if rolling back
            migrationBuilder.Sql(@"
                UPDATE users.""Users"" u
                SET 
                    ""FirstName"" = COALESCE(hp.first_name, ''),
                    ""LastName"" = COALESCE(hp.last_name, ''),
                    ""MiddleName"" = hp.middle_name
                FROM hr.hr_profiles hp
                WHERE u.""Id"" = hp.user_id;
            ");
        }
    }
}
