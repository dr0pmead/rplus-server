using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.HR.Infrastructure.Migrations
{
    public partial class AddHrFileDocumentId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "document_id",
                schema: "hr",
                table: "hr_files",
                type: "uuid",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "document_id",
                schema: "hr",
                table: "hr_files");
        }
    }
}
