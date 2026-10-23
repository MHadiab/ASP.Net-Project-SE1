using Ledger_and_Inventory_Service.Entities;
using Microsoft.EntityFrameworkCore;

namespace Ledger_and_Inventory_Service.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options):DbContext(options)
{
    public DbSet<Hold> Holds => Set<Hold>();





    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Hold>(entity =>
        {   
            entity.ToTable("holds");
            
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasColumnName("id");
            
            entity.Property(e=>e.AccountId)
                .HasColumnName("account_id")
                .IsRequired();
            
            //todo:
            // entity.HasOne(e => e.Account)
            //     .WithMany() //  .WithMany(a => a.Holds) اگر در اکانت ها هولد را نگه مداشتیم باید این قسمت را عوض کنیم به
            //     .HasForeignKey(e => e.AccountId)
            //     .OnDelete(DeleteBehavior.Restrict); 
            
            entity.Property(e => e.Currency)
                .HasColumnName("currency")
                .HasMaxLength(3)
                .HasDefaultValue("IRR")
                .IsRequired();
            
            entity.Property(e => e.Status)
                .HasColumnName("status")
                .HasConversion<string>() // ذخیره وضعیت به صورت رشته در دیتابیس
                .IsRequired();
            
            entity.Property(e => e.ReferenceType)
                .HasColumnName("reference_type")
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(e => e.ReferenceId)
                .HasColumnName("reference_id")
                .IsRequired();

            entity.Property(e => e.ExpiresAt)
                .HasColumnName("expires_at")
                .IsRequired();

            entity.Property(e => e.CapturedEntryId)
                .HasColumnName("captured_entry_id");

            entity.Property(e => e.ReleaseReason)
                .HasColumnName("release_reason")
                .HasMaxLength(200);

            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            entity.Property(e =>e.ResolvedAt)
                .HasColumnName("resolved_at");

            entity.Property(e =>e.CorrelationId)
                .HasColumnName("correlation_id")
                .HasMaxLength(64)
                .IsRequired();
            
            entity.Property(e => e.Xmin)
                .HasColumnName("xmin")
                .IsRowVersion(); //به دیتابیس نشان میدهد این فیلد برای همزمانی هست
            
            // ۱. قید بررسی مثبت بودن مبلغ (CHECK constraint)
            entity.ToTable(t => t.HasCheckConstraint("CK_holds_amount_positive", "amount > 0"));
            
            //  قید یکتایی برای جلوگیری از ثبت تکراری و Double-spend روی شناسه انتقال
            entity.HasIndex(e => new { e.ReferenceType, e.ReferenceId })
                .IsUnique()
                .HasDatabaseName("uq_holds_reference");
            
            //ایندکس برای بررسی سریع هولد های فعال و انقضای خودکار
            entity.HasIndex(e => new { e.Status, e.ExpiresAt })
                .HasDatabaseName("ix_holds_status_expires_at");
            
            
        });
    }
}