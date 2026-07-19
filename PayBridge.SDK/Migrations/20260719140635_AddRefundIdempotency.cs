using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayBridge.SDK.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Refunds",
                type: "nvarchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "Refunds",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_IdempotencyKey",
                table: "Refunds",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Refunds_IdempotencyKey",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Refunds");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "Refunds");
        }
    }
}
