using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.HR.Infrastructure.Migrations
{
    public partial class AddCustomFieldIsSystem : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_system",
                schema: "hr",
                table: "hr_custom_fields",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_hr_custom_fields_is_system",
                schema: "hr",
                table: "hr_custom_fields",
                column: "is_system");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_hr_custom_fields_is_system",
                schema: "hr",
                table: "hr_custom_fields");

            migrationBuilder.DropColumn(
                name: "is_system",
                schema: "hr",
                table: "hr_custom_fields");
        }
    }
}
