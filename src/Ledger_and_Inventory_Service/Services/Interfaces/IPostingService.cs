using System.Threading;
using System.Threading.Tasks;
using Ledger_and_Inventory_Service.Entities;

namespace Ledger_and_Inventory_Service.Services.Interfaces;

public interface IPostingService
{
    Task<JournalEntry> PostAsync(
        JournalEntry entry,
        CancellationToken cancellationToken = default);
}