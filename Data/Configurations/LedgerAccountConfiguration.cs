using Ledger_and_Inventory_Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger_and_Inventory_Service.Data.Configurations;

public class LedgerAccountConfiguration
    : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.ToTable("ledger_accounts", table =>
        {
            table.HasCheckConstraint(
                "ck_ledger_accounts_currency",
                "currency = 'IRR'");

            table.HasCheckConstraint(
                "ck_ledger_accounts_account_type",
                "account_type IN ('CUSTOMER', 'SYSTEM')");

            table.HasCheckConstraint(
                "ck_ledger_accounts_status",
                "status IN ('ACTIVE', 'FROZEN', 'CLOSED')");

            // only SYSTEM accounts may go negative
            table.HasCheckConstraint(
                "ck_ledger_accounts_allow_negative",
                "allow_negative = false OR account_type = 'SYSTEM'");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.AccountType)
            .HasColumnName("account_type")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.SystemCode)
            .HasColumnName("system_code")
            .HasMaxLength(32);

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasColumnType("character(3)")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(x => x.AllowNegative)
            .HasColumnName("allow_negative");

        builder.Property(x => x.StatusReason)
            .HasColumnName("status_reason")
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.SystemCode)
            .IsUnique()
            .HasFilter("system_code IS NOT NULL");
    }
}
