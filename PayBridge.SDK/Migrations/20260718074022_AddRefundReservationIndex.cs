using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayBridge.SDK.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundReservationIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PaymentTransactionReference",
                table: "Refunds",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_PaymentTransactionReference_Status",
                table: "Refunds",
                columns: new[] { "PaymentTransactionReference", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Refunds_PaymentTransactionReference_Status",
                table: "Refunds");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentTransactionReference",
                table: "Refunds",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");
        }
    }
}
