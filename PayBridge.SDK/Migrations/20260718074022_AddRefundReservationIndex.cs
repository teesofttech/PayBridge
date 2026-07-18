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
            var (boundedType, unboundedType) = GetProviderColumnTypes();
            migrationBuilder.AlterColumn<string>(
                name: "PaymentTransactionReference",
                table: "Refunds",
                type: boundedType,
                nullable: false,
                oldClrType: typeof(string),
                oldType: unboundedType);

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

            var (boundedType, unboundedType) = GetProviderColumnTypes();
            migrationBuilder.AlterColumn<string>(
                name: "PaymentTransactionReference",
                table: "Refunds",
                type: unboundedType,
                nullable: false,
                oldClrType: typeof(string),
                oldType: boundedType);
        }

        private (string Bounded, string Unbounded) GetProviderColumnTypes()
        {
            if (ActiveProvider.Contains("Npgsql"))
            {
                return ("character varying(450)", "text");
            }

            if (ActiveProvider.Contains("MySql"))
            {
                return ("varchar(450)", "longtext");
            }

            if (ActiveProvider.Contains("Sqlite"))
            {
                return ("TEXT", "TEXT");
            }

            return ("nvarchar(450)", "nvarchar(max)");
        }
    }
}
