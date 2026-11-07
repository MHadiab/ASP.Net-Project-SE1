using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Enums;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services;
using Ledger.Tests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;


public class HoldServiceTests
{
    private static HoldService CreateService(AppDbContext db)
    {
        var logger = Mock.Of<ILogger<HoldService>>();
        return new HoldService(db, logger);
    }

    private static CreateHoldRequest CreateRequest(
        Guid accountId,
        long amount = 5000,
        string currency = "IRR",
        Guid? transferId = null)
    {
        return new CreateHoldRequest(
            accountId,
            new MoneyDto(amount, currency),
            transferId ?? Guid.NewGuid(),
            3600,
            "Test"
        );
    }

    private static async Task<Guid> SeedAccountAsync(
        AppDbContext db,
        bool postingAllowed = true,
        bool createBalance = true,
        long bookBalance = 100_000,
        long heldAmount = 0)
    {
        var accountId = Guid.NewGuid();

        var account = new LedgerAccount
        {
            Id = accountId,
            AccountType = AccountType.CUSTOMER,
            SystemCode = null,
            Currency = "IRR",
            PostingAllowed = postingAllowed,
            AllowNegative = false
        };

        db.LedgerAccounts.Add(account);

        if (createBalance)
        {
            var balance = new AccountBalance
            {
                AccountId = accountId,
                BookBalance = bookBalance,
                HeldAmount = heldAmount,
                Version = 1
            };

            db.AccountBalances.Add(balance);
        }

        await db.SaveChangesAsync();

        return accountId;
    }

    private static async Task CleanupAsync(
        AppDbContext db,
        Guid accountId)
    {
        var holds = await db.Holds
            .Where(h => h.AccountId == accountId)
            .ToListAsync();

        db.Holds.RemoveRange(holds);

        var balance = await db.AccountBalances
            .FindAsync(accountId);

        if (balance is not null)
            db.AccountBalances.Remove(balance);

        var account = await db.LedgerAccounts
            .FindAsync(accountId);

        if (account is not null)
            db.LedgerAccounts.Remove(account);

        await db.SaveChangesAsync();
    }

    // 1. Successful hold creation

    [Fact]
    public async Task CreateHold_WithValidAmount_ShouldCreateHold()
    {
        await using var db = TestDatabase.CreateContext();

        var accountId = await SeedAccountAsync(db);

        try
        {
            var service = CreateService(db);
            var request = CreateRequest(accountId);

            var result = await service.CreateHoldAsync(
                request,
                "test-correlation",
                null,
                CancellationToken.None
            );

            Assert.NotEqual(Guid.Empty, result.HoldId);
            Assert.Equal(accountId, result.AccountId);
            Assert.Equal(5000, result.Amount.Amount);
            Assert.Equal("IRR", result.Amount.Currency);
            Assert.Equal("ACTIVE", result.Status);
            Assert.Equal(request.TransferId, result.TransferId);

            var updatedBalance = await db.AccountBalances
                .FindAsync(accountId);

            Assert.NotNull(updatedBalance);
            Assert.Equal(5000, updatedBalance.HeldAmount);
            Assert.Equal(100_000, updatedBalance.BookBalance);
            Assert.Equal(2, updatedBalance.Version);

            var savedHold = await db.Holds
                .SingleOrDefaultAsync(h => h.Id == result.HoldId);
            
            
            Assert.NotNull(savedHold);
        }
        finally
        {
            await CleanupAsync(db, accountId);
        }
    }

    // 2. Zero and negative amount

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public async Task CreateHold_WithInvalidAmount_ShouldThrowValidation(
        long amount)
    {
        await using var db = TestDatabase.CreateContext();

        var service = CreateService(db);

        var request = CreateRequest(
            Guid.NewGuid(),
            amount
        );

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.CreateHoldAsync(
                request,
                "test-correlation",
                null,
                CancellationToken.None
            )
        );
    }

    // 3. Account does not exist

    [Fact]
    public async Task CreateHold_WithNonExistingAccount_ShouldThrowNotFound()
    {
        await using var db = TestDatabase.CreateContext();

        var service = CreateService(db);

        var request = CreateRequest(Guid.NewGuid());

        await Assert.ThrowsAsync<AppException>(
            () => service.CreateHoldAsync(
                request,
                "test-correlation",
                null,
                CancellationToken.None
            )
        );
    }

    // 4. Account posting is blocked

    [Fact]
    public async Task CreateHold_WithBlockedAccount_ShouldThrowAccountBlocked()
    {
        await using var db = TestDatabase.CreateContext();

        var accountId = await SeedAccountAsync(
            db,
            postingAllowed: false
        );

        try
        {
            var service = CreateService(db);
            var request = CreateRequest(accountId);

            await Assert.ThrowsAsync<AppException>(
                () => service.CreateHoldAsync(
                    request,
                    "test-correlation",
                    null,
                    CancellationToken.None
                )
            );
        }
        finally
        {
            await CleanupAsync(db, accountId);
        }
    }

    // 5. Currency mismatch

    [Fact]
    public async Task CreateHold_WithDifferentCurrency_ShouldThrowCurrencyMismatch()
    {
        await using var db = TestDatabase.CreateContext();

        var accountId = await SeedAccountAsync(db);

        try
        {
            var service = CreateService(db);
            var request = CreateRequest(
                accountId,
                currency: "USD"
            );

            await Assert.ThrowsAsync<AppException>(
                () => service.CreateHoldAsync(
                    request,
                    "test-correlation",
                    null,
                    CancellationToken.None
                )
            );
        }
        finally
        {
            await CleanupAsync(db, accountId);
        }
    }

    // 6. Insufficient available balance

    [Fact]
    public async Task CreateHold_WithInsufficientFunds_ShouldThrowInsufficientFunds()
    {
        await using var db = TestDatabase.CreateContext();

        var accountId = await SeedAccountAsync(
            db,
            bookBalance: 1000
        );

        try
        {
            var service = CreateService(db);
            var request = CreateRequest(accountId, amount: 5000);

            await Assert.ThrowsAsync<AppException>(
                () => service.CreateHoldAsync(
                    request,
                    "test-correlation",
                    null,
                    CancellationToken.None
                )
            );

            var balance = await db.AccountBalances
                .FindAsync(accountId);

            Assert.NotNull(balance);
            Assert.Equal(0, balance.HeldAmount);
        }
        finally
        {
            await CleanupAsync(db, accountId);
        }
    }

    // 7. Account exists but balance does not

    [Fact]
    public async Task CreateHold_WithoutAccountBalance_ShouldThrowInsufficientFunds()
    {
        await using var db = TestDatabase.CreateContext();

        var accountId = await SeedAccountAsync(
            db,
            createBalance: false
        );

        try
        {
            var service = CreateService(db);
            var request = CreateRequest(accountId);
            await Assert.ThrowsAsync<AppException>(
                () => service.CreateHoldAsync(
                    request,
                    "test-correlation",
                    null,
                    CancellationToken.None
                )
            );
        }
        finally
        {
            await CleanupAsync(db, accountId);
        }
    }

    // 8. Duplicate request with same parameters

    [Fact]
    public async Task CreateHold_WithDuplicateRequest_ShouldReturnExistingHold()
    {
        await using var db = TestDatabase.CreateContext();

        var accountId = await SeedAccountAsync(db);

        try
        {
            var service = CreateService(db);
            var transferId = Guid.NewGuid();

            var request = CreateRequest(
                accountId,
                amount: 5000,
                transferId: transferId
            );

            var firstResult = await service.CreateHoldAsync(
                request,
                "correlation-1",
                null,
                CancellationToken.None
            );

            var secondResult = await service.CreateHoldAsync(
                request,
                "correlation-2",
                null,
                CancellationToken.None
            );

            Assert.Equal(firstResult.HoldId, secondResult.HoldId);

            var balance = await db.AccountBalances
                .FindAsync(accountId);

            Assert.NotNull(balance);
            Assert.Equal(5000, balance.HeldAmount);

            var holdCount = await db.Holds
                .CountAsync(h => h.AccountId == accountId);

            Assert.Equal(1, holdCount);
        }
        finally
        {
            await CleanupAsync(db, accountId);
        }
    }

    // 9. Duplicate transfer ID with different parameters

    [Fact]
    public async Task CreateHold_WithDuplicateTransferIdAndDifferentAmount_ShouldThrowConflict()
    {
        await using var db = TestDatabase.CreateContext();

        var accountId = await SeedAccountAsync(db);

        try
        {
            var service = CreateService(db);
            var transferId = Guid.NewGuid();

            var firstRequest = CreateRequest(
                accountId,
                amount: 5000,
                transferId: transferId
            );

            await service.CreateHoldAsync(
                firstRequest,
                "test-correlation",
                null,
                CancellationToken.None
            );

            var secondRequest = CreateRequest(
                accountId,
                amount: 6000,
                transferId: transferId
            );

            await Assert.ThrowsAsync<AppException>(
                () => service.CreateHoldAsync(
                    secondRequest,
                    "test-correlation",
                    null,
                    CancellationToken.None
                )
            );

            var balance = await db.AccountBalances
                .FindAsync(accountId);

            Assert.NotNull(balance);
            Assert.Equal(5000, balance.HeldAmount);
        }
        finally
        {
            await CleanupAsync(db, accountId);
        }
    }
}
