using System.Globalization;
using System.Text.RegularExpressions;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Errors;

namespace Ledger_and_Inventory_Service.Validators;

public static class JournalEntriesQueryValidator
{
    private static readonly Regex UtcTimestampPattern = new(
        @"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(\.[0-9]+)?Z$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    public static (
        JournalDirection? Direction,
        DateTimeOffset? From
    ) ValidateAndParse(JournalEntriesQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.Limit is < 1 or > 200)
        {
            throw Invalid("limit must be between 1 and 200.");
        }

        JournalDirection? direction = query.Direction switch
        {
            null => null,
            "DEBIT" => JournalDirection.DEBIT,
            "CREDIT" => JournalDirection.CREDIT,
            _ => throw Invalid("direction must be DEBIT or CREDIT.")
        };

        DateTimeOffset? from = null;

        if (query.From is not null)
        {
            if (!UtcTimestampPattern.IsMatch(query.From) ||
                !DateTimeOffset.TryParse(
                    query.From,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal |
                    DateTimeStyles.AdjustToUniversal,
                    out var parsedFrom))
            {
                throw Invalid(
                    "from must be a valid UTC timestamp, " +
                    "for example 2026-10-31T00:00:00Z.");
            }

            from = parsedFrom;
        }

        if (query.Cursor is not null &&
            string.IsNullOrWhiteSpace(query.Cursor))
        {
            throw Invalid("cursor must not be empty.");
        }

        return (direction, from);
    }

    private static AppException Invalid(string detail)
    {
        return new AppException(
            "ERROR_VALIDATION",
            400,
            "Invalid journal entries query",
            detail);
    }
}
