using Ledger_and_Inventory_Service.Data;
using Ledger_and_Inventory_Service.DTOs;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Errors;
using Ledger_and_Inventory_Service.Services.Interfaces;
using Ledger_and_Inventory_Service.Validators;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ledger_and_Inventory_Service.Services;

public class JournalEntriesService(AppDbContext db)
    : IJournalEntriesService
{
    public async Task<JournalEntriesResponse> GetAsync(
        Guid accountId,
        JournalEntriesQuery query,
        CancellationToken cancellationToken = default)
    {
        var (direction, from) =
            JournalEntriesQueryValidator.ValidateAndParse(query);

        var cursor = query.Cursor is null
            ? null
            : JournalEntriesCursorCodec.Decode(
                query.Cursor, accountId, direction, from);

        try
        {
            var journalLines = await BuildFilteredQueryAsync(
                accountId, direction, from, cancellationToken);

            if (cursor is not null)
            {
                var lastPostedAt = cursor.PostedAt;
                var lastEntryNumber = cursor.EntryNumber;
                var lastLineNo = cursor.LineNo;

                journalLines = journalLines.Where(line =>
                    line.PostedAt < lastPostedAt ||
                    (line.PostedAt == lastPostedAt &&
                        line.Entry.EntryNumber < lastEntryNumber) ||
                    (line.PostedAt == lastPostedAt &&
                        line.Entry.EntryNumber == lastEntryNumber &&
                        line.LineNo < lastLineNo));
            }

            var rows = await journalLines
                .OrderByDescending(line => line.PostedAt)
                .ThenByDescending(line => line.Entry.EntryNumber)
                .ThenByDescending(line => line.LineNo)
                .Select(line => new
                {
                    EntryId = line.EntryId,
                    EntryNumber = line.Entry.EntryNumber,
                    LineNo = line.LineNo,
                    PostedAt = line.PostedAt,
                    Direction = line.Direction,
                    Amount = line.Amount,
                    BalanceAfter = line.BalanceAfter,
                    EntryType = line.Entry.EntryType,
                    ReferenceId = line.Entry.ReferenceId,
                    Description = line.Entry.Description,
                    IsReversed = line.Entry.ReversedByEntry != null
                })
                .Take(query.Limit + 1)
                .ToListAsync(cancellationToken);

            var hasNextPage = rows.Count > query.Limit;

            var items = rows
                .Take(query.Limit)
                .Select(row => new JournalEntryItemDto(
                    row.EntryId,
                    row.EntryNumber,
                    row.LineNo,
                    row.PostedAt,
                    row.Direction.ToString(),
                    row.Amount,
                    row.BalanceAfter,
                    row.EntryType.ToString(),
                    row.ReferenceId,
                    row.Description,
                    row.IsReversed))
                .ToArray();

            string? nextCursor = null;

            if (hasNextPage)
            {
                var lastItem = items[^1];

                nextCursor = JournalEntriesCursorCodec.Encode(
                    new JournalEntriesCursor(
                        1,
                        accountId,
                        direction,
                        from,
                        lastItem.PostedAt,
                        lastItem.EntryNumber,
                        lastItem.LineNo));
            }

            return new JournalEntriesResponse(
                accountId,
                items,
                nextCursor);
        }
        catch (NpgsqlException exception) when (exception.IsTransient)
        {
            throw new AppException(
                "SERVICE_UNAVAILABLE",
                503,
                "Database unavailable",
                "Account entries cannot be retrieved right now.");
        }
    }

    private async Task<IQueryable<JournalLine>> BuildFilteredQueryAsync(
        Guid accountId,
        JournalDirection? direction,
        DateTimeOffset? from,
        CancellationToken cancellationToken)
    {
        var accountExists = await db.LedgerAccounts
            .AsNoTracking()
            .AnyAsync(
                account => account.Id == accountId,
                cancellationToken);

        if (!accountExists)
        {
            throw new AppException(
                "NOT_FOUND",
                404,
                "Account not found",
                $"No ledger account exists with id {accountId}.");
        }

        var journalLines = db.JournalLines
            .AsNoTracking()
            .Where(line => line.AccountId == accountId);

        if (direction.HasValue)
        {
            var requestedDirection = direction.Value;

            journalLines = journalLines.Where(
                line => line.Direction == requestedDirection);
        }

        if (from.HasValue)
        {
            var requestedFrom = from.Value;

            journalLines = journalLines.Where(
                line => line.PostedAt >= requestedFrom);
        }

        return journalLines;
    }
}
