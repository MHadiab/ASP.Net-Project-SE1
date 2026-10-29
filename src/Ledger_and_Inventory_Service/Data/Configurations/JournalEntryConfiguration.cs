using Ledger_and_Inventory_Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger_and_Inventory_Service.Data.Configurations;

public class JournalEntryConfiguration
    : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries", table =>
        {
            table.HasCheckConstraint(
                "ck_journal_entries_currency",
                "currency = 'IRR'");

            table.HasCheckConstraint(
                "ck_journal_entries_total_amount",
                "total_amount > 0");

            table.HasCheckConstraint(
                "ck_journal_entries_entry_type",
                """
                entry_type IN (
                    'TRANSFER_INTERNAL',
                    'TRANSFER_EXTERNAL',
                    'OPENING_BALANCE',
                    'ADJUSTMENT',
                    'REVERSAL'
                )
                """);

            table.HasCheckConstraint(
                "ck_journal_entries_reference_type",
                "reference_type IN ('TRANSFER', 'HOLD', 'MANUAL')");

            table.HasCheckConstraint(
                "ck_journal_entries_reversal",
                """
                (
                    entry_type = 'REVERSAL'
                    AND reverses_entry_id IS NOT NULL
                    AND reversal_reason IS NOT NULL
                    AND length(trim(reversal_reason)) > 0
                )
                OR
                (
                    entry_type <> 'REVERSAL'
                    AND reverses_entry_id IS NULL
                    AND reversal_reason IS NULL
                )
                """);

            table.HasCheckConstraint(
                "ck_journal_entries_no_self_reversal",
                "reverses_entry_id IS NULL OR reverses_entry_id <> id");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.EntryNumber)
            .HasColumnName("entry_number")
            .UseIdentityByDefaultColumn();

        builder.Property(x => x.EntryType)
            .HasColumnName("entry_type")
            .HasConversion<string>()
            .HasMaxLength(24)
            .IsRequired();

        builder.Property(x => x.ReferenceType)
            .HasColumnName("reference_type")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.ReferenceId)
            .HasColumnName("reference_id");

        builder.Property(x => x.HoldId)
            .HasColumnName("hold_id");

        builder.Property(x => x.ReversesEntryId)
            .HasColumnName("reverses_entry_id");

        builder.Property(x => x.ReversalReason)
            .HasColumnName("reversal_reason")
            .HasMaxLength(300);

        builder.Property(x => x.Description)
            .HasColumnName("description")
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasColumnName("currency")
            .HasColumnType("character(3)")
            .IsRequired();

        builder.Property(x => x.TotalAmount)
            .HasColumnName("total_amount");

        builder.Property(x => x.PostedAt)
            .HasColumnName("posted_at")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.BusinessDate)
            .HasColumnName("business_date")
            .HasColumnType("date");

        builder.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(x => x.EntryNumber)
            .IsUnique();

        builder.HasIndex(x => x.ReferenceId);

        builder.HasIndex(x => x.BusinessDate);

        builder.HasIndex(x => x.ReversesEntryId)
            .IsUnique()
            .HasFilter("reverses_entry_id IS NOT NULL");

        builder.HasOne(x => x.ReversesEntry)
            .WithOne(x => x.ReversedByEntry)
            .HasForeignKey<JournalEntry>(x => x.ReversesEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
