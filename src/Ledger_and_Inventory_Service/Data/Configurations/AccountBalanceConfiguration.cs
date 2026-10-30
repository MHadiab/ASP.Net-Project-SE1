using Ledger_and_Inventory_Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger_and_Inventory_Service.Data.Configurations;

public class AccountBalanceConfiguration : IEntityTypeConfiguration<AccountBalance>
{
    public void Configure(EntityTypeBuilder<AccountBalance> builder)
    {
        builder.ToTable("account_balances", table =>
        {
            table.HasCheckConstraint("ck_account_balances_held_amount",
                "held_amount >= 0");
        });

        builder.HasKey(x => x.AccountId);

        builder.Property(x => x.AccountId)
            .HasColumnName("account_id")
            .ValueGeneratedNever();

        builder.Property(x => x.BookBalance)
            .HasColumnName("balance_book");

        builder.Property(x => x.HeldAmount)
            .HasColumnName("held_amount");

        builder.Property(x => x.Version)
            .HasColumnName("version");

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");

        builder.Ignore(x => x.AvailableBalance);

        builder.HasOne(x => x.Account)
            .WithOne(x => x.Balance)
            .HasForeignKey<AccountBalance>(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}