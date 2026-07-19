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
            var (keyType, fingerprintType) = GetProviderColumnTypes();
            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Refunds",
                type: keyType,
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestFingerprint",
                table: "Refunds",
                type: fingerprintType,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_Refunds_IdempotencyKey",
                table: "Refunds",
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
