using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger_and_Inventory_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddLedgerAccountsAndBalances : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ledger_accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    system_code = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    currency = table.Column<string>(type: "character(3)", nullable: false),
                    posting_allowed = table.Column<bool>(type: "boolean", nullable: false),
                    allow_negative = table.Column<bool>(type: "boolean", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_accounts", x => x.id);
                    table.CheckConstraint("ck_ledger_accounts_account_type", "account_type IN ('SYSTEM', 'CUSTOMER')");
                    table.CheckConstraint("ck_ledger_accounts_allow_negative", "allow_negative = false OR account_type = 'SYSTEM'");
                    table.CheckConstraint("ck_ledger_accounts_currency", "currency = 'IRR'");
                    table.CheckConstraint("ck_ledger_accounts_system_code", "system_code IS NULL OR system_code IN ('CLEARING', 'CASH_ISSUANCE')");
                    table.CheckConstraint("ck_ledger_accounts_system_shape", "(account_type = 'SYSTEM' AND system_code IS NOT NULL)\r\nOR\r\n(account_type = 'CUSTOMER' AND system_code IS NULL)");
                });

            migrationBuilder.CreateTable(
                name: "account_balances",
                columns: table => new
                {
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    balance_book = table.Column<long>(type: "bigint", nullable: false),
                    held_amount = table.Column<long>(type: "bigint", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_account_balances", x => x.account_id);
                    table.CheckConstraint("ck_account_balances_held_amount", "held_amount >= 0");
                    table.ForeignKey(
                        name: "FK_account_balances_ledger_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "ledger_accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ledger_accounts_system_code",
                table: "ledger_accounts",
                column: "system_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "account_balances");

            migrationBuilder.DropTable(
                name: "ledger_accounts");
        }
    }
}
