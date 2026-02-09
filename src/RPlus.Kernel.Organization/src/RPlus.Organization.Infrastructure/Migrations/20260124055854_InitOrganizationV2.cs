using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.Organization.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitOrganizationV2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "organization");

            migrationBuilder.CreateTable(
                name: "org_nodes",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Type = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Path = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Attributes = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_org_nodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_org_nodes_org_nodes_ParentId",
                        column: x => x.ParentId,
                        principalSchema: "organization",
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "node_contexts",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    InheritanceStrategy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_node_contexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_node_contexts_org_nodes_NodeId",
                        column: x => x.NodeId,
                        principalSchema: "organization",
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ReportsToPositionId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsVacant = table.Column<bool>(type: "boolean", nullable: false),
                    Attributes = table.Column<JsonDocument>(type: "jsonb", nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_positions_org_nodes_NodeId",
                        column: x => x.NodeId,
                        principalSchema: "organization",
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_positions_positions_ReportsToPositionId",
                        column: x => x.ReportsToPositionId,
                        principalSchema: "organization",
                        principalTable: "positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "position_contexts",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Data = table.Column<JsonDocument>(type: "jsonb", nullable: false),
                    InheritanceStrategy = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_position_contexts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_position_contexts_positions_PositionId",
                        column: x => x.PositionId,
                        principalSchema: "organization",
                        principalTable: "positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_assignments",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    PositionId = table.Column<Guid>(type: "uuid", nullable: false),
                    NodeId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReplacementForAssignmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    FtePercent = table.Column<decimal>(type: "numeric", nullable: false),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_assignments_org_nodes_NodeId",
                        column: x => x.NodeId,
                        principalSchema: "organization",
                        principalTable: "org_nodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_assignments_positions_PositionId",
                        column: x => x.PositionId,
                        principalSchema: "organization",
                        principalTable: "positions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_user_assignments_user_assignments_ReplacementForAssignmentId",
                        column: x => x.ReplacementForAssignmentId,
                        principalSchema: "organization",
                        principalTable: "user_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_role_overrides",
                schema: "organization",
                columns: table => new
                {
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    ValidFrom = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidTo = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_role_overrides", x => new { x.AssignmentId, x.RoleCode });
                    table.ForeignKey(
                        name: "FK_user_role_overrides_user_assignments_AssignmentId",
                        column: x => x.AssignmentId,
                        principalSchema: "organization",
                        principalTable: "user_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_node_contexts_NodeId",
                schema: "organization",
                table: "node_contexts",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_node_contexts_TenantId_NodeId_ResourceType_ValidTo",
                schema: "organization",
                table: "node_contexts",
                columns: new[] { "TenantId", "NodeId", "ResourceType", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_ParentId",
                schema: "organization",
                table: "org_nodes",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_IsDeleted_ValidTo",
                schema: "organization",
                table: "org_nodes",
                columns: new[] { "TenantId", "IsDeleted", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_ParentId",
                schema: "organization",
                table: "org_nodes",
                columns: new[] { "TenantId", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_org_nodes_TenantId_Path",
                schema: "organization",
                table: "org_nodes",
                columns: new[] { "TenantId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_position_contexts_PositionId",
                schema: "organization",
                table: "position_contexts",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_position_contexts_TenantId_PositionId_ResourceType_ValidTo",
                schema: "organization",
                table: "position_contexts",
                columns: new[] { "TenantId", "PositionId", "ResourceType", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_positions_NodeId",
                schema: "organization",
                table: "positions",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_positions_ReportsToPositionId",
                schema: "organization",
                table: "positions",
                column: "ReportsToPositionId");

            migrationBuilder.CreateIndex(
                name: "IX_positions_TenantId_NodeId_IsDeleted",
                schema: "organization",
                table: "positions",
                columns: new[] { "TenantId", "NodeId", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_positions_TenantId_ReportsToPositionId",
                schema: "organization",
                table: "positions",
                columns: new[] { "TenantId", "ReportsToPositionId" });

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_NodeId",
                schema: "organization",
                table: "user_assignments",
                column: "NodeId");

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_PositionId",
                schema: "organization",
                table: "user_assignments",
                column: "PositionId");

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_ReplacementForAssignmentId",
                schema: "organization",
                table: "user_assignments",
                column: "ReplacementForAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_TenantId_NodeId_IsDeleted_ValidTo",
                schema: "organization",
                table: "user_assignments",
                columns: new[] { "TenantId", "NodeId", "IsDeleted", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_TenantId_PositionId_IsDeleted_ValidTo",
                schema: "organization",
                table: "user_assignments",
                columns: new[] { "TenantId", "PositionId", "IsDeleted", "ValidTo" });

            migrationBuilder.CreateIndex(
                name: "IX_user_assignments_TenantId_UserId_IsDeleted_ValidTo",
                schema: "organization",
                table: "user_assignments",
                columns: new[] { "TenantId", "UserId", "IsDeleted", "ValidTo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "node_contexts",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "position_contexts",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "user_role_overrides",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "user_assignments",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "positions",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "org_nodes",
                schema: "organization");
        }
    }
}
