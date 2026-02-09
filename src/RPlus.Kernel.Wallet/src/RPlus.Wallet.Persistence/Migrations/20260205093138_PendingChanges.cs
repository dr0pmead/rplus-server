using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RPlus.Wallet.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PendingChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Month",
                table: "transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SourceCategory",
                table: "transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceType",
                table: "transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Year",
                table: "transactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_user_month_source",
                table: "transactions",
                columns: new[] { "UserId", "Year", "Month", "SourceType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_transactions_user_month_source",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "Month",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "SourceCategory",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "Year",
                table: "transactions");
        }
    }
}
