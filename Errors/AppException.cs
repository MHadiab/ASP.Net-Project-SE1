
namespace Ledger_and_Inventory_Service.Errors;

public class AppException(string code, int status, string title,
    string? detail = null)
    : Exception(detail ?? title)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
    public string Title { get; } = title;
}
