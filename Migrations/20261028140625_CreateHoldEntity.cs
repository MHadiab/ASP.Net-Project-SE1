using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ledger_and_Inventory_Service.Migrations
{
    /// <inheritdoc />
    public partial class CreateHoldEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "holds",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<long>(type: "bigint", nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false, defaultValue: "IRR"),
                    status = table.Column<string>(type: "text", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    captured_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    release_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_holds", x => x.id);
                    table.CheckConstraint("CK_holds_amount_positive", "amount > 0");
                });

            migrationBuilder.CreateIndex(
                name: "ix_holds_status_expires_at",
                table: "holds",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "uq_holds_reference",
                table: "holds",
                columns: new[] { "reference_type", "reference_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "holds");
        }
    }
}
