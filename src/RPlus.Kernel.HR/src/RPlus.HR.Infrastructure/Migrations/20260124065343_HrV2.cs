using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.HR.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HrV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "pk_employee_profiles",
                schema: "hr",
                table: "employee_profiles");

            migrationBuilder.RenameTable(
                name: "employee_profiles",
                schema: "hr",
                newName: "hr_profiles",
                newSchema: "hr");

            migrationBuilder.RenameColumn(
                name: "phone",
                schema: "hr",
                table: "hr_profiles",
                newName: "personal_phone");

            migrationBuilder.RenameIndex(
                name: "ix_employee_profiles_status",
                schema: "hr",
                table: "hr_profiles",
                newName: "ix_hr_profiles_status");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Active",
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AddColumn<string>(
                name: "blood_type",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "citizenship",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(2)",
                maxLength: 2,
                nullable: false,
                defaultValue: "KZ");

            migrationBuilder.AddColumn<string>(
                name: "clothing_size",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "disability_group",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<string>(
                name: "first_name",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "gender",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "iin",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "last_name",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "middle_name",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "residential_address_flat",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "place_of_birth",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_address_building",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_address_city",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_address_district",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_address_flat",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_address_region",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "registration_address_street",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "residential_address_building",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "residential_address_city",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "residential_address_district",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "residential_address_region",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "residential_address_street",
                schema: "hr",
                table: "hr_profiles",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "shoe_size",
                schema: "hr",
                table: "hr_profiles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "pk_hr_profiles",
                schema: "hr",
                table: "hr_profiles",
                column: "user_id");

            migrationBuilder.CreateTable(
                name: "hr_audit_logs",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    actor_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_service = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    entity_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    entity_id = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    action = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    changes_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_audit_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hr_bank_details",
                schema: "hr",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    iban = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    bank_name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_bank_details", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_hr_bank_details_hr_profiles_user_id",
                        column: x => x.user_id,
                        principalSchema: "hr",
                        principalTable: "hr_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "hr_documents",
                schema: "hr",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    series = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    number = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expiry_date = table.Column<DateOnly>(type: "date", nullable: true),
                    issued_by = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    scan_file_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    full_name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    birth_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_dependent = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
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
                name: "hr_military_records",
                schema: "hr",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_liable = table.Column<bool>(type: "boolean", nullable: false),
                    rank = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    vus_code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    local_office = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hr_military_records", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_hr_military_records_hr_profiles_user_id",
                        column: x => x.user_id,
                        principalSchema: "hr",
                        principalTable: "hr_profiles",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hr_profiles_iin",
                schema: "hr",
                table: "hr_profiles",
                column: "iin",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hr_audit_logs_actor_user_id",
                schema: "hr",
                table: "hr_audit_logs",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_hr_audit_logs_entity_type",
                schema: "hr",
                table: "hr_audit_logs",
                column: "entity_type");

            migrationBuilder.CreateIndex(
                name: "ix_hr_audit_logs_occurred_at",
                schema: "hr",
                table: "hr_audit_logs",
                column: "occurred_at");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hr_audit_logs",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "hr_bank_details",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "hr_documents",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "hr_family_members",
                schema: "hr");

            migrationBuilder.DropTable(
                name: "hr_military_records",
                schema: "hr");

            migrationBuilder.DropPrimaryKey(
                name: "pk_hr_profiles",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropIndex(
                name: "ix_hr_profiles_iin",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "blood_type",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "citizenship",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "clothing_size",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "disability_group",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "first_name",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "gender",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "iin",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "last_name",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "middle_name",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "place_of_birth",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "registration_address_building",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "registration_address_city",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "registration_address_district",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "registration_address_flat",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "registration_address_region",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "registration_address_street",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "residential_address_building",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "residential_address_city",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "residential_address_district",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "residential_address_region",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "residential_address_street",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "residential_address_flat",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.DropColumn(
                name: "shoe_size",
                schema: "hr",
                table: "hr_profiles");

            migrationBuilder.RenameTable(
                name: "hr_profiles",
                schema: "hr",
                newName: "employee_profiles",
                newSchema: "hr");

            migrationBuilder.RenameColumn(
                name: "personal_phone",
                schema: "hr",
                table: "employee_profiles",
                newName: "phone");

            migrationBuilder.RenameIndex(
                name: "ix_hr_profiles_status",
                schema: "hr",
                table: "employee_profiles",
                newName: "ix_employee_profiles_status");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                schema: "hr",
                table: "employee_profiles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32,
                oldDefaultValue: "Active");

            migrationBuilder.AddPrimaryKey(
                name: "pk_employee_profiles",
                schema: "hr",
                table: "employee_profiles",
                column: "user_id");
        }
    }
}
