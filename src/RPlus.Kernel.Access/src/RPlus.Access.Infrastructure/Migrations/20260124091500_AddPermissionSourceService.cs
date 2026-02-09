using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.Access.Infrastructure.Migrations;

public partial class AddPermissionSourceService : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "source_service",
            schema: "access",
            table: "features",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_features_source_service",
            schema: "access",
            table: "features",
            column: "source_service");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_features_source_service",
            schema: "access",
            table: "features");

        migrationBuilder.DropColumn(
            name: "source_service",
            schema: "access",
            table: "features");
    }
}

