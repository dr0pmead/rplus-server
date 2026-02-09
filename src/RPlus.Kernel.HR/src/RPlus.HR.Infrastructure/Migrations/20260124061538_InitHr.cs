using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.HR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitHr : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hr");

            migrationBuilder.CreateTable(
                name: "employee_profiles",
                schema: "hr",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: true),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    personal_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_employee_profiles", x => x.user_id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_employee_profiles_status",
                schema: "hr",
                table: "employee_profiles",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_profiles",
                schema: "hr");
        }
    }
}
