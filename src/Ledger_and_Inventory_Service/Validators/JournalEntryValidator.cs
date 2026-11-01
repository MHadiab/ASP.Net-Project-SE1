using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Errors;

namespace Ledger_and_Inventory_Service.Validators;

public static class JournalEntryValidator
{
    public static long ValidateAndGetTotal(JournalEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (entry.Currency != "IRR")
        {
            throw new AppException(
                "CURRENCY_MISMATCH", 400,
                "Unsupported currency",
                "Journal currency must be IRR.");
        }

        if (entry.Lines is null || entry.Lines.Count < 2)
        {
            throw new AppException(
                "UNBALANCED_ENTRY", 400,
                "Invalid journal entry",
                "At least two journal lines are required.");
        }

        long debitTotal = 0;
        long creditTotal = 0;

        try
        {
            foreach (var line in entry.Lines)
            {
                if (line is null || line.Amount <= 0)
                {
                    throw new AppException(
                        "UNBALANCED_ENTRY", 400,
                        "Invalid journal line",
                        "Each line must have a positive amount.");
                }

                switch (line.Direction)
                {
                    case JournalDirection.DEBIT:
                        debitTotal = checked(debitTotal + line.Amount);
                        break;

                    case JournalDirection.CREDIT:
                        creditTotal = checked(creditTotal + line.Amount);
                        break;

                    default:
                        throw new AppException(
                            "UNBALANCED_ENTRY", 400,
                            "Invalid journal direction",
                            "Direction must be DEBIT or CREDIT.");
                }
            }
        }
        catch (OverflowException)
        {
            throw new AppException(
                "UNBALANCED_ENTRY", 400,
                "Journal total is too large",
                "The total exceeds the supported amount range.");
        }

        if (debitTotal == 0 || creditTotal == 0 ||
            debitTotal != creditTotal)
        {
            throw new AppException(
                "UNBALANCED_ENTRY", 400,
                "Unbalanced journal entry",
                "Debit and credit totals must be equal and positive.");
        }

        return debitTotal;
    }
}
