using Ledger_and_Inventory_Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger_and_Inventory_Service.Data.Configurations;

public class CaptureReceiptConfiguration : IEntityTypeConfiguration<CaptureReceipt>
{
    public void Configure(EntityTypeBuilder<CaptureReceipt> builder)
    {
        builder.ToTable("capture_receipts");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasColumnName("key").HasMaxLength(128);
        builder.Property(x => x.RequestHash).HasColumnName("request_hash")
            .HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponseJson).HasColumnName("response_json")
            .HasColumnType("text").IsRequired();
    }
}
