using Ledger_and_Inventory_Service.DTOs;

namespace Ledger_and_Inventory_Service.Services.Interfaces;

public interface IJournalEntriesService
{
    Task<JournalEntriesResponse> GetAsync(
        Guid accountId,
        JournalEntriesQuery query,
        CancellationToken cancellationToken = default
    );
}