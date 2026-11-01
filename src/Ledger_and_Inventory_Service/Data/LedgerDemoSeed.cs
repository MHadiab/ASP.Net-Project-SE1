using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Ledger_and_Inventory_Service.Data;

public static class LedgerDemoSeed
{
    private static readonly Guid SourceId =
        Guid.Parse("0192a7d0-0000-7000-8000-000000000001");

    private static readonly Guid DestinationId =
        Guid.Parse("0192a7d0-0000-7000-8000-000000000002");

    private static readonly Guid OpeningEntryId =
        Guid.Parse("0192a7e4-0000-7000-8000-000000000001");

    private static readonly Guid FirstTransferId =
        Guid.Parse("0192a7e4-0000-7000-8000-000000000002");

    private static readonly Guid SecondTransferId =
        Guid.Parse("0192a7e4-0000-7000-8000-000000000003");

    public static async Task RunAsync(
        AppDbContext db,
        IPostingService postingService,
        CancellationToken cancellationToken = default)
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        var entryIds = new[]
        {
            OpeningEntryId,
            FirstTransferId,
            SecondTransferId
        };

        var existingCount = await db.JournalEntries
            .CountAsync(
                entry => entryIds.Contains(entry.Id),
                cancellationToken);

        if (existingCount == entryIds.Length)
        {
            return;
        }

        if (existingCount != 0)
        {
            throw new InvalidOperationException(
                "Demo journal identifiers already exist partially.");
        }

        var demoAccountExists = await db.LedgerAccounts.AnyAsync(
            account => account.Id == SourceId ||
                       account.Id == DestinationId,
            cancellationToken);

        if (demoAccountExists)
        {
            throw new InvalidOperationException(
                "Demo account identifiers are already in use.");
        }

        var issuance = await db.LedgerAccounts.SingleOrDefaultAsync(
            account => account.SystemCode ==
                       SystemAccountCode.CASH_ISSUANCE,
            cancellationToken);

        if (issuance is null)
        {
            issuance = CreateAccount(
                Guid.CreateVersion7(),
                AccountType.SYSTEM);

            issuance.SystemCode = SystemAccountCode.CASH_ISSUANCE;
            issuance.AllowNegative = true;

            db.LedgerAccounts.Add(issuance);
        }

        db.LedgerAccounts.AddRange(
            CreateAccount(SourceId, AccountType.CUSTOMER),
            CreateAccount(DestinationId, AccountType.CUSTOMER));

        await db.SaveChangesAsync(cancellationToken);

        await postingService.PostAsync(
            CreateEntry(
                OpeningEntryId,
                issuance.Id,
                SourceId,
                1_000_000,
                JournalEntryType.OPENING_BALANCE,
                "S04 demo opening balance"),
            cancellationToken);

        await postingService.PostAsync(
            CreateEntry(
                FirstTransferId,
                SourceId,
                DestinationId,
                100_000,
                JournalEntryType.TRANSFER_INTERNAL,
                "S04 demo first transfer"),
            cancellationToken);

        await postingService.PostAsync(
            CreateEntry(
                SecondTransferId,
                SourceId,
                DestinationId,
                150_000,
                JournalEntryType.TRANSFER_INTERNAL,
                "S04 demo second transfer"),
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }

    private static LedgerAccount CreateAccount(
        Guid accountId,
        AccountType accountType)
    {
        return new LedgerAccount
        {
            Id = accountId,
            AccountType = accountType,
            Balance = new AccountBalance
            {
                AccountId = accountId
            }
        };
    }

    private static JournalEntry CreateEntry(
        Guid entryId,
        Guid sourceId,
        Guid destinationId,
        long amount,
        JournalEntryType entryType,
        string description)
    {
        return new JournalEntry
        {
            Id = entryId,
            EntryType = entryType,
            ReferenceType = entryType == JournalEntryType.OPENING_BALANCE
                ? "MANUAL"
                : "TRANSFER",
            ReferenceId = entryId,
            Description = description,
            CorrelationId = "s04-demo-seed",
            CreatedBy = "LedgerDemoSeed",
            Lines = new List<JournalLine>
            {
                new()
                {
                    AccountId = sourceId,
                    Direction = JournalDirection.DEBIT,
                    Amount = amount
                },
                new()
                {
                    AccountId = destinationId,
                    Direction = JournalDirection.CREDIT,
                    Amount = amount
                }
            }
        };
    }
}
