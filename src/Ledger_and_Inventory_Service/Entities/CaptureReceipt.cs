namespace Ledger_and_Inventory_Service.Entities;

// Committed together with the hold and journal; only successful captures are replayed.
public class CaptureReceipt
{
    public string Key { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string ResponseJson { get; set; } = string.Empty;
}
