using System.Text.Json;
using Ledger_and_Inventory_Service.Entities;
using Ledger_and_Inventory_Service.Errors;
using Microsoft.AspNetCore.WebUtilities;

namespace Ledger_and_Inventory_Service.Services;

internal record JournalEntriesCursor(
    int Version,
    Guid AccountId,
    JournalDirection? Direction,
    DateTimeOffset? From,
    DateTimeOffset PostedAt,
    long EntryNumber,
    short LineNo
);

internal static class JournalEntriesCursorCodec
{
    public static string Encode(JournalEntriesCursor cursor)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(cursor);

        return WebEncoders.Base64UrlEncode(bytes);
    }

    public static JournalEntriesCursor Decode(
        string value,
        Guid accountId,
        JournalDirection? direction,
        DateTimeOffset? from)
    {
        if (value.Length > 4096)
        {
            throw Invalid();
        }

        JournalEntriesCursor? cursor;

        try
        {
            var bytes = WebEncoders.Base64UrlDecode(value);

            cursor =
                JsonSerializer.Deserialize<JournalEntriesCursor>(bytes);
        }
        catch (Exception exception) when (
            exception is FormatException or
            JsonException or
            ArgumentException)
        {
            throw Invalid();
        }

        if (cursor is null ||
            cursor.Version != 1 ||
            cursor.AccountId != accountId ||
            cursor.Direction != direction ||
            cursor.From != from ||
            cursor.PostedAt == default ||
            cursor.PostedAt.Offset != TimeSpan.Zero ||
            cursor.EntryNumber <= 0 ||
            cursor.LineNo <= 0)
        {
            throw Invalid();
        }

        return cursor;
    }

    private static AppException Invalid()
    {
        return new AppException(
            "ERROR_VALIDATION",
            400,
            "Invalid pagination cursor",
            "cursor is invalid or does not match the account and filters.");
    }
}
