using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Ledger_and_Inventory_Service.Migrations
{
    /// <inheritdoc />
    public partial class AddJournalEntriesAndLines : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "journal_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_number = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    entry_type = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    reference_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    hold_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reverses_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reversal_reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    currency = table.Column<string>(type: "character(3)", nullable: false),
                    total_amount = table.Column<long>(type: "bigint", nullable: false),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    business_date = table.Column<DateOnly>(type: "date", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_by = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_entries", x => x.id);
                    table.CheckConstraint("ck_journal_entries_currency", "currency = 'IRR'");
                    table.CheckConstraint("ck_journal_entries_entry_type", "entry_type IN (\r\n    'TRANSFER_INTERNAL',\r\n    'TRANSFER_EXTERNAL',\r\n    'OPENING_BALANCE',\r\n    'ADJUSTMENT',\r\n    'REVERSAL'\r\n)");
                    table.CheckConstraint("ck_journal_entries_no_self_reversal", "reverses_entry_id IS NULL OR reverses_entry_id <> id");
                    table.CheckConstraint("ck_journal_entries_reference_type", "reference_type IN ('TRANSFER', 'HOLD', 'MANUAL')");
                    table.CheckConstraint("ck_journal_entries_reversal", "(\r\n    entry_type = 'REVERSAL'\r\n    AND reverses_entry_id IS NOT NULL\r\n    AND reversal_reason IS NOT NULL\r\n    AND length(trim(reversal_reason)) > 0\r\n)\r\nOR\r\n(\r\n    entry_type <> 'REVERSAL'\r\n    AND reverses_entry_id IS NULL\r\n    AND reversal_reason IS NULL\r\n)");
                    table.CheckConstraint("ck_journal_entries_total_amount", "total_amount > 0");
                    table.ForeignKey(
                        name: "FK_journal_entries_journal_entries_reverses_entry_id",
                        column: x => x.reverses_entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "journal_lines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    line_no = table.Column<short>(type: "smallint", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    direction = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    balance_after = table.Column<long>(type: "bigint", nullable: false),
                    posted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_journal_lines", x => x.id);
                    table.CheckConstraint("ck_journal_lines_amount", "amount > 0");
                    table.CheckConstraint("ck_journal_lines_direction", "direction IN ('DEBIT', 'CREDIT')");
                    table.CheckConstraint("ck_journal_lines_line_no", "line_no > 0");
                    table.ForeignKey(
                        name: "FK_journal_lines_journal_entries_entry_id",
                        column: x => x.entry_id,
                        principalTable: "journal_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_business_date",
                table: "journal_entries",
                column: "business_date");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_entry_number",
                table: "journal_entries",
                column: "entry_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_reference_id",
                table: "journal_entries",
                column: "reference_id");

            migrationBuilder.CreateIndex(
                name: "IX_journal_entries_reverses_entry_id",
                table: "journal_entries",
                column: "reverses_entry_id",
                unique: true,
                filter: "reverses_entry_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_account_id_posted_at_id",
                table: "journal_lines",
                columns: new[] { "account_id", "posted_at", "id" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "IX_journal_lines_entry_id_line_no",
                table: "journal_lines",
                columns: new[] { "entry_id", "line_no" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "journal_lines");

            migrationBuilder.DropTable(
                name: "journal_entries");
        }
    }
}
