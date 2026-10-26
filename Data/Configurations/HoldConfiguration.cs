using Ledger_and_Inventory_Service.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ledger_and_Inventory_Service.Data.Configurations;

public class HoldConfiguration : IEntityTypeConfiguration<Hold>
{
    public void Configure(EntityTypeBuilder<Hold> builder)
    {
        builder.ToTable("holds");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        //todo:
        // builder.HasOne(e => e.Account)
        //     .WithMany() //  .WithMany(a => a.Holds) اگر در اکانت ها هولد را نگه مداشتیم باید این قسمت را عوض کنیم به
        //     .HasForeignKey(e => e.AccountId)
        //     .OnDelete(DeleteBehavior.Restrict); 

        builder.Property(e => e.Currency)
            .HasColumnName("currency")
            .HasMaxLength(3)
            .HasDefaultValue("IRR")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>() // ذخیره وضعیت به صورت رشته در دیتابیس
            .IsRequired();

        builder.Property(e => e.ReferenceType)
            .HasColumnName("reference_type")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ReferenceId)
            .HasColumnName("reference_id")
            .IsRequired();

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at")
            .IsRequired();

        builder.Property(e => e.CapturedEntryId)
            .HasColumnName("captured_entry_id");

        builder.Property(e => e.ReleaseReason)
            .HasColumnName("release_reason")
            .HasMaxLength(200);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(e => e.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(e => e.Xmin)
            .HasColumnName("xmin")
            .IsRowVersion(); //به دیتابیس نشان میدهد این فیلد برای همزمانی هست

        //  قید بررسی مثبت بودن مبلغ (CHECK constraint)
        builder.ToTable(t => t.HasCheckConstraint("CK_holds_amount_positive", "amount > 0"));

        //  قید یکتایی برای جلوگیری از ثبت تکراری و Double-spend روی شناسه انتقال
        builder.HasIndex(e => new { e.ReferenceType, e.ReferenceId })
            .IsUnique()
            .HasDatabaseName("uq_holds_reference");

        //ایندکس برای بررسی سریع هولد های فعال و انقضای خودکار
        builder.HasIndex(e => new { e.Status, e.ExpiresAt })
            .HasDatabaseName("ix_holds_status_expires_at");
    }
}