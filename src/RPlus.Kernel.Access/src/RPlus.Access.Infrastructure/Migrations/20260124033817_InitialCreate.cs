using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "access");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:ltree", ",,");

            migrationBuilder.CreateTable(
                name: "applications",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "effective_snapshots",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    context = table.Column<string>(type: "text", nullable: false),
                    data_json = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    calculated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_effective_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "integration_api_key_permissions",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    api_key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<string>(type: "text", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_api_key_permissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "local_user_assignments",
                schema: "access",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    path_snapshot = table.Column<string>(type: "ltree", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_local_user_assignments", x => new { x.tenant_id, x.user_id, x.node_id, x.role_code });
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "root_registry",
                schema: "access",
                columns: table => new
                {
                    HashedUserId = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_root_registry", x => x.HashedUserId);
                });

            migrationBuilder.CreateTable(
                name: "ServiceRegistry",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    BaseUrl = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    PublicKeys = table.Column<string>(type: "text", nullable: false),
                    Criticality = table.Column<int>(type: "integer", nullable: false),
                    LastSeen = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceRegistry", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sod_policy_sets",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sod_policy_sets", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "features",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    app_id = table.Column<Guid>(type: "uuid", nullable: false),
                    supported_contexts = table.Column<string[]>(type: "text[]", nullable: false),
                    resource = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "ACTIVE")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_features", x => x.id);
                    table.ForeignKey(
                        name: "fk_permissions_apps_app_id",
                        column: x => x.app_id,
                        principalSchema: "access",
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SodPolicies",
                schema: "access",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicySetId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConflictRoles = table.Column<List<string>>(type: "text[]", nullable: false),
                    Scope = table.Column<string>(type: "text", nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SodPolicies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SodPolicies_sod_policy_sets_PolicySetId",
                        column: x => x.PolicySetId,
                        principalSchema: "access",
                        principalTable: "sod_policy_sets",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "access_policies",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false, defaultValue: new Guid("00000000-0000-0000-0000-000000000000")),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permission_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    effect = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "ALLOW"),
                    scope_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    condition_expression = table.Column<string>(type: "text", nullable: true),
                    priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    required_auth_level = table.Column<int>(type: "integer", nullable: true),
                    max_auth_age_seconds = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_access_policies", x => x.id);
                    table.ForeignKey(
                        name: "FK_access_policies_features_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "access",
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_access_policies_roles_role_id",
                        column: x => x.role_id,
                        principalSchema: "access",
                        principalTable: "roles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "policy_assignments",
                schema: "access",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    permission_id = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    effect = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false, defaultValue: "ALLOW"),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_policy_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_policy_assignments_features_permission_id",
                        column: x => x.permission_id,
                        principalSchema: "access",
                        principalTable: "features",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_access_policies_permission_id",
                schema: "access",
                table: "access_policies",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_access_policies_role_id",
                schema: "access",
                table: "access_policies",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_applications_code",
                schema: "access",
                table: "applications",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_effective_snapshots_user_id_tenant_id_context",
                schema: "access",
                table: "effective_snapshots",
                columns: new[] { "user_id", "tenant_id", "context" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_features_app_id",
                schema: "access",
                table: "features",
                column: "app_id");

            migrationBuilder.CreateIndex(
                name: "IX_features_id",
                schema: "access",
                table: "features",
                column: "id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_integration_api_key_permissions_api_key_id_permission_id",
                schema: "access",
                table: "integration_api_key_permissions",
                columns: new[] { "api_key_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_local_user_assignments_path_snapshot",
                schema: "access",
                table: "local_user_assignments",
                column: "path_snapshot")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "IX_policy_assignments_permission_id",
                schema: "access",
                table: "policy_assignments",
                column: "permission_id");

            migrationBuilder.CreateIndex(
                name: "IX_policy_assignments_tenant_id_target_type_target_id_permissi~",
                schema: "access",
                table: "policy_assignments",
                columns: new[] { "tenant_id", "target_type", "target_id", "permission_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_roles_code",
                schema: "access",
                table: "roles",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SodPolicies_PolicySetId",
                schema: "access",
                table: "SodPolicies",
                column: "PolicySetId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "access_policies",
                schema: "access");

            migrationBuilder.DropTable(
                name: "effective_snapshots",
                schema: "access");

            migrationBuilder.DropTable(
                name: "integration_api_key_permissions",
                schema: "access");

            migrationBuilder.DropTable(
                name: "local_user_assignments",
                schema: "access");

            migrationBuilder.DropTable(
                name: "policy_assignments",
                schema: "access");

            migrationBuilder.DropTable(
                name: "root_registry",
                schema: "access");

            migrationBuilder.DropTable(
                name: "ServiceRegistry",
                schema: "access");

            migrationBuilder.DropTable(
                name: "SodPolicies",
                schema: "access");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "access");

            migrationBuilder.DropTable(
                name: "features",
                schema: "access");

            migrationBuilder.DropTable(
                name: "sod_policy_sets",
                schema: "access");

            migrationBuilder.DropTable(
                name: "applications",
                schema: "access");
        }
    }
}
