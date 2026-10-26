using Ledger_and_Inventory_Service.Enums;

namespace Ledger_and_Inventory_Service.Entities;
public class Hold
{
    public Guid Id { get; set; } = Guid.CreateVersion7(); 
    
    public Guid AccountId { get; set; }
    
    //todo
    // اضافه‌کردن Navigation Property برای دسترسی راحت‌تر به آبجکت حساب
    // public LedgerAccount Account { get; set; } = null!;

    public long Amount { get; set; }
    public string Currency { get; set; } = "IRR";
    public HoldStatus Status { get; set; } = HoldStatus.Active;
    
    public string ReferenceType { get; set; } = "TRANSFER";
    public Guid ReferenceId { get; set; } // همان TransferId
    
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid? CapturedEntryId { get; set; }
    public string? ReleaseReason { get; set; }
    
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    
    public string CorrelationId { get; set; } = string.Empty;

    //row version
    public uint Xmin { get; set; }
    
    
    public bool IsActive => Status == HoldStatus.Active && DateTimeOffset.UtcNow < ExpiresAt;

    public void Capture(Guid entryId)
    {
        if (Status != HoldStatus.Active)
            throw new InvalidOperationException("Hold is not active.");
            
        Status = HoldStatus.Captured;
        CapturedEntryId = entryId;
        ResolvedAt = DateTimeOffset.UtcNow;
    }

    public void Release(string? reason = null)
    {
        if (Status != HoldStatus.Active)
            throw new InvalidOperationException("Hold is not active.");
            
        Status = HoldStatus.Released;
        ReleaseReason = reason;
        ResolvedAt = DateTimeOffset.UtcNow;
    }

    public void Expire()
    {
        if (Status == HoldStatus.Active)
        {
            Status = HoldStatus.Expired;
            ResolvedAt = DateTimeOffset.UtcNow;
        }
    }
}