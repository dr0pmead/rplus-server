using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.Access.Infrastructure.Migrations;

public partial class AddIntegrationApiKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "integration_api_keys",
            schema: "access",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                application_id = table.Column<Guid>(type: "uuid", nullable: false),
                name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                environment = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_integration_api_keys", x => x.id);
                table.ForeignKey(
                    name: "fk_integration_api_keys_applications_application_id",
                    column: x => x.application_id,
                    principalSchema: "access",
                    principalTable: "applications",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_integration_api_keys_application_id",
            schema: "access",
            table: "integration_api_keys",
            column: "application_id");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "integration_api_keys",
            schema: "access");
    }
}

