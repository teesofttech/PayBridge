using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PayBridge.SDK.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var (keyType, fingerprintType) = GetProviderColumnTypes();
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Transactions",
                type: keyType,
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "Transactions",
                type: fingerprintType,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_IdempotencyKey",
                table: "Transactions",
                column: "IdempotencyKey",
                unique: true,
                filter: ActiveProvider.Contains("SqlServer")
                    ? "[IdempotencyKey] IS NOT NULL"
                    : null);
        }

        private (string Key, string Fingerprint) GetProviderColumnTypes()
        {
            if (ActiveProvider.Contains("Npgsql"))
            {
                return ("character varying(255)", "character varying(64)");
            }

            if (ActiveProvider.Contains("MySql"))
            {
                return ("varchar(255)", "varchar(64)");
            }

            if (ActiveProvider.Contains("Sqlite"))
            {
                return ("TEXT", "TEXT");
            }

            return ("nvarchar(255)", "nvarchar(64)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_IdempotencyKey",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "RequestFingerprint",
                table: "Transactions");
        }
    }
}
