using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.Access.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerUserLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "partner_user_links",
                schema: "access",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_partner_user_links", x => new { x.application_id, x.user_id });
                });

            migrationBuilder.CreateIndex(
                name: "IX_partner_user_links_user_id",
                schema: "access",
                table: "partner_user_links",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "partner_user_links",
                schema: "access");
        }
    }
}
