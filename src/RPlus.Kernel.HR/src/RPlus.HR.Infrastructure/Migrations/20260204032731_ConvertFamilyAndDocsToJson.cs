using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.HR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConvertFamilyAndDocsToJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_documents",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "hr_family_documents",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "hr_family_members",
                schema: "hr");

            migrationBuilder.AddColumn<string>(
                name: "documents",
                schema: "hr",
                table: "hr_profiles",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "family_members",
                schema: "hr",
                table: "hr_profiles",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "documents",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "family_members",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.CreateTable(
                name: "hr_documents",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    issued_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    scan_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    series = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_documents", x => x.id);
                    table.ForeignKey(
                        name: "fk_hr_documents_hr_profiles_user_id",
                        column: x => x.user_id,
                        principalSchema: "hr",
                        principalTable: "hr_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hr_family_members",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    full_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    is_dependent = table.Column<bool>(type: "boolean", nullable: false),
                    relation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_family_members", x => x.id);
                    table.ForeignKey(
                        name: "fk_hr_family_members_hr_profiles_user_id",
                        column: x => x.user_id,
                        principalSchema: "hr",
                        principalTable: "hr_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hr_family_documents",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    file_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
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
                name: "ix_hr_documents_type",
                schema: "hr",
                table: "hr_documents",
                column: "type");

            migrationBuilder.CreateIndex(
                name: "ix_hr_documents_user_id",
                schema: "hr",
                table: "hr_documents",
                column: "user_id");

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

            migrationBuilder.CreateIndex(
                name: "ix_hr_family_members_relation",
                schema: "hr",
                table: "hr_family_members",
                column: "relation");

            migrationBuilder.CreateIndex(
                name: "ix_hr_family_members_user_id",
                schema: "hr",
                table: "hr_family_members",
                column: "user_id");
        }
    }
}
