using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.HR.Infrastructure.Migrations
{
    public partial class AddCustomFields : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hr_custom_fields",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    label = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    required = table.Column<bool>(type: "boolean", nullable: false),
                    group = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    min_length = table.Column<int>(type: "integer", nullable: true),
                    max_length = table.Column<int>(type: "integer", nullable: true),
                    pattern = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    placeholder = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    options_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_custom_fields", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hr_custom_values",
                schema: "hr",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    value_json = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_custom_values", x => new { x.user_id, x.field_key });
                });

            migrationBuilder.CreateIndex(
                name: "ix_hr_custom_fields_group",
                schema: "hr",
                table: "hr_custom_fields",
                column: "group");

            migrationBuilder.CreateIndex(
                name: "ix_hr_custom_fields_is_active",
                schema: "hr",
                table: "hr_custom_fields",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_hr_custom_fields_key",
                schema: "hr",
                table: "hr_custom_fields",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hr_custom_values_field_key",
                schema: "hr",
                table: "hr_custom_values",
                column: "field_key");

            migrationBuilder.CreateIndex(
                name: "ix_hr_custom_values_user_id",
                schema: "hr",
                table: "hr_custom_values",
                column: "user_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_custom_values",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "hr_custom_fields",
                schema: "hr");
        }
    }
}
