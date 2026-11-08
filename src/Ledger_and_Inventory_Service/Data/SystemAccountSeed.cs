using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Microsoft.EntityFrameworkCore;

namespace Ledger_and_Inventory_Service.Data;

// Creates the two system accounts (US-1) if they do not exist yet.
// Only system accounts may have a negative balance, so the whole ledger always sums to zero.
public static class SystemAccountSeed
{
    public static async Task EnsureAsync(
        AppDbContext db,
        CancellationToken ct = default)
    {
        foreach (var code in Enum.GetValues<SystemAccountCode>())
        {
            var exists = await db.LedgerAccounts
                .AnyAsync(a => a.SystemCode == code, ct);

            if (exists) continue;

            var id = Guid.CreateVersion7();
            var now = DateTimeOffset.UtcNow;

            db.LedgerAccounts.Add(new LedgerAccount
            {
                Id = id,
                AccountType = AccountType.SYSTEM,
                SystemCode = code,
                AllowNegative = true,
                CreatedAt = now,
                UpdatedAt = now,
                Balance = new AccountBalance { AccountId = id, UpdatedAt = now }
            });
        }

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another instance seeded at the same moment (unique system_code); nothing to do.
            db.ChangeTracker.Clear();
        }
    }
}