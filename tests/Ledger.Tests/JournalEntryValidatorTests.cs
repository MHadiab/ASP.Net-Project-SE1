using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Validators;
using Xunit;

namespace Ledger.Tests;

public class JournalEntryValidatorTests
{
    [Theory]
    [InlineData(100_000L)]
    [InlineData(250_000L)]
    [InlineData(long.MaxValue)]
    public void Balanced_entry_returns_total_of_one_side(long amount)
    {
        var entry = CreateEntry(amount, amount);

        var total = JournalEntryValidator.ValidateAndGetTotal(entry);

        Assert.Equal(amount, total);
    }

    [Theory]
    [InlineData(100_000L, 90_000L)]
    [InlineData(0L, 0L)]
    [InlineData(-100L, -100L)]
    public void Invalid_amounts_or_totals_are_rejected(
        long debit, long credit)
    {
        var entry = CreateEntry(debit, credit);

        var error = Assert.Throws<AppException>(
            () => JournalEntryValidator.ValidateAndGetTotal(entry));

        Assert.Equal("UNBALANCED_ENTRY", error.Code);
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public void Unsupported_currency_is_rejected()
    {
        var entry = CreateEntry(100_000, 100_000);
        entry.Currency = "USD";

        var error = Assert.Throws<AppException>(
            () => JournalEntryValidator.ValidateAndGetTotal(entry));

        Assert.Equal("CURRENCY_MISMATCH", error.Code);
        Assert.Equal(400, error.Status);
    }

    [Fact]
    public void Overflow_is_rejected_as_an_application_error()
    {
        var entry = CreateEntry(long.MaxValue, long.MaxValue);

        entry.Lines.Add(new JournalLine
        {
            Direction = JournalDirection.DEBIT,
            Amount = 1
        });

        entry.Lines.Add(new JournalLine
        {
            Direction = JournalDirection.CREDIT,
            Amount = 1
        });

        var error = Assert.Throws<AppException>(
            () => JournalEntryValidator.ValidateAndGetTotal(entry));

        Assert.Equal("UNBALANCED_ENTRY", error.Code);
        Assert.Equal(400, error.Status);
    }

    private static JournalEntry CreateEntry(long debit, long credit)
    {
        return new JournalEntry
        {
            Currency = "IRR",
            Lines = new List<JournalLine>
            {
                new()
                {
                    Direction = JournalDirection.DEBIT,
                    Amount = debit
                },
                new()
                {
                    Direction = JournalDirection.CREDIT,
                    Amount = credit
                }
            }
        };
    }
}
