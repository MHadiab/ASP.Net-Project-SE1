using Ledger_and_Inventory_Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger_and_Inventory_Service.Data.Configurations;

public class JournalLineConfiguration
    : IEntityTypeConfiguration<JournalLine>
{
    public void Configure(EntityTypeBuilder<JournalLine> builder)
    {
        builder.ToTable("journal_lines", table =>
        {
            table.HasCheckConstraint(
                "ck_journal_lines_amount",
                "amount > 0");

            table.HasCheckConstraint(
                "ck_journal_lines_line_no",
                "line_no > 0");

            table.HasCheckConstraint(
                "ck_journal_lines_direction",
                "direction IN ('DEBIT', 'CREDIT')");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.EntryId)
            .HasColumnName("entry_id");

        builder.Property(x => x.LineNo)
            .HasColumnName("line_no");

        builder.Property(x => x.AccountId)
            .HasColumnName("account_id");

        builder.Property(x => x.Direction)
            .HasColumnName("direction")
            .HasConversion<string>()
            .HasMaxLength(6)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasColumnName("amount");

        builder.Property(x => x.BalanceAfter)
            .HasColumnName("balance_after");

        builder.Property(x => x.PostedAt)
            .HasColumnName("posted_at")
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(x => new { x.EntryId, x.LineNo })
            .IsUnique();

        builder.HasIndex(x => new
            {
                x.AccountId,
                x.PostedAt,
                x.Id
            })
            .IsDescending(false, true, false);

        builder.HasOne(x => x.Entry)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.EntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
