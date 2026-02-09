using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.HR.Infrastructure.Migrations
{
    public partial class HrFilesAndFamilyDocs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "photo_file_id",
                schema: "hr",
                table: "hr_profiles",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "hr_files",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_files", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hr_family_documents",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_family_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_hr_family_documents_hr_family_members_family_member_id",
                        column: x => x.family_member_id,
                        principalSchema: "hr",
                        principalTable: "hr_family_members",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hr_profiles_photo_file_id",
                schema: "hr",
                table: "hr_profiles",
                column: "photo_file_id");

            migrationBuilder.CreateIndex(
                name: "ix_hr_files_owner_user_id",
                schema: "hr",
                table: "hr_files",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_hr_family_documents_family_member_id",
                schema: "hr",
                table: "hr_family_documents",
                column: "family_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_hr_family_documents_file_id",
                schema: "hr",
                table: "hr_family_documents",
                column: "file_id");

            migrationBuilder.CreateIndex(
                name: "ix_hr_family_documents_user_id",
                schema: "hr",
                table: "hr_family_documents",
                column: "user_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_family_documents",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "hr_files",
                schema: "hr");

            migrationBuilder.DropIndex(
                name: "ix_hr_profiles_photo_file_id",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "photo_file_id",
                schema: "hr",
                table: "hr_profiles");
        }
    }
}
