using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Ledger_and_Inventory_Service.Services;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Ledger.Tests;

public class JournalEntriesServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:17").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString()).Options);

    [Theory]
    [InlineData("DEBIT")]
    [InlineData("CREDIT")]
    public async Task Reversal_state_is_current_even_when_reversing_line_is_filtered_out(
        string direction)
    {
        var issuance = NewAccount(AccountType.SYSTEM);
        issuance.SystemCode = SystemAccountCode.CASH_ISSUANCE;
        issuance.AllowNegative = true;
        var source = NewAccount(AccountType.CUSTOMER);
        var destination = NewAccount(AccountType.CUSTOMER);

        await using (var setup = CreateContext())
        {
            setup.LedgerAccounts.AddRange(issuance, source, destination);
            await setup.SaveChangesAsync();
        }

        var opening = Transfer(issuance.Id, source.Id, 100);
        opening.EntryType = JournalEntryType.OPENING_BALANCE;
        opening.ReferenceType = "MANUAL";
        await PostAsync(opening);

        var original = Transfer(source.Id, destination.Id, 30);
        await PostAsync(original);

        var accountId = direction == "DEBIT" ? source.Id : destination.Id;
        var query = new JournalEntriesQuery(Direction: direction);

        await using (var beforeContext = CreateContext())
        {
            var before = await new JournalEntriesService(beforeContext)
                .GetAsync(accountId, query);
            var originalItem = Assert.Single(before.Items);
            Assert.Equal(original.Id, originalItem.EntryId);
            Assert.False(originalItem.IsReversed);
        }

        var reversal = Transfer(destination.Id, source.Id, 30);
        reversal.EntryType = JournalEntryType.REVERSAL;
        reversal.ReversesEntryId = original.Id;
        reversal.ReversalReason = "Transfer failed";
        await PostAsync(reversal);

        await using var afterContext = CreateContext();
        var service = new JournalEntriesService(afterContext);
        var filtered = await service.GetAsync(accountId, query);
        var filteredItem = Assert.Single(filtered.Items);
        Assert.Equal(original.Id, filteredItem.EntryId);
        Assert.Equal(direction, filteredItem.Direction);
        Assert.True(filteredItem.IsReversed);

        var unfiltered = await service.GetAsync(accountId, new JournalEntriesQuery());
        var reversalItem = Assert.Single(unfiltered.Items,
            item => item.EntryId == reversal.Id);
        Assert.Equal("REVERSAL", reversalItem.EntryType);
        Assert.False(reversalItem.IsReversed);
    }

    private async Task PostAsync(JournalEntry entry)
    {
        await using var db = CreateContext();
        await new PostingService(db).PostAsync(entry);
    }

    private static LedgerAccount NewAccount(AccountType accountType)
    {
        var accountId = Guid.CreateVersion7();
        return new LedgerAccount
        {
            Id = accountId,
            AccountType = accountType,
            Balance = new AccountBalance { AccountId = accountId }
        };
    }

    private static JournalEntry Transfer(Guid sourceId, Guid destinationId, long amount) => new()
    {
        EntryType = JournalEntryType.TRANSFER_INTERNAL,
        ReferenceType = "TRANSFER",
        ReferenceId = Guid.CreateVersion7(),
        Description = "Reversal history test",
        CorrelationId = "journal-history-tests",
        CreatedBy = "Ledger.Tests",
        Lines = new List<JournalLine>
        {
            new() { AccountId = sourceId, Direction = JournalDirection.DEBIT, Amount = amount },
            new() { AccountId = destinationId, Direction = JournalDirection.CREDIT, Amount = amount }
        }
    };
}
