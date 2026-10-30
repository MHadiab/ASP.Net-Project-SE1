using Ledger_and_Inventory_Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger_and_Inventory_Service.Data.Configurations;

public class LedgerAccountConfiguration : IEntityTypeConfiguration<LedgerAccount>
{
    public void Configure(EntityTypeBuilder<LedgerAccount> builder)
    {
        builder.ToTable("ledger_accounts", table =>
        {
            table.HasCheckConstraint("ck_ledger_accounts_currency",
                "currency = 'IRR'");

            table.HasCheckConstraint("ck_ledger_accounts_account_type",
                "account_type IN ('SYSTEM', 'CUSTOMER')");

            table.HasCheckConstraint("ck_ledger_accounts_system_code",
                "system_code IS NULL OR system_code IN ('CLEARING', 'CASH_ISSUANCE')");

            // SYSTEM accounts must have a system_code, CUSTOMER accounts must not
            table.HasCheckConstraint("ck_ledger_accounts_system_shape",
                """
                (account_type = 'SYSTEM' AND system_code IS NOT NULL)
                OR
                (account_type = 'CUSTOMER' AND system_code IS NULL)
                """);

            // Only SYSTEM accounts may allow a negative balance
            table.HasCheckConstraint("ck_ledger_accounts_allow_negative",
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
            .HasConversion<string>()
            .HasMaxLength(32);

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasColumnType("character(3)")
            .IsRequired();

        builder.Property(x => x.PostingAllowed)
            .HasColumnName("posting_allowed")
            .IsRequired();

        builder.Property(x => x.AllowNegative)
            .HasColumnName("allow_negative")
            .IsRequired();

        builder.Property(x => x.Reason)
            .HasColumnName("reason")
            .HasMaxLength(200);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.Xmin)
            .HasColumnName("xmin")
            .IsRowVersion();

        // NULLs don't collide in Postgres, so CUSTOMER accounts are fine
        builder.HasIndex(x => x.SystemCode)
            .IsUnique();
    }
}