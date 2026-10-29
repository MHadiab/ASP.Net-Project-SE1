// using Ledger_and_Inventory_Service.Data;
// using Ledger_and_Inventory_Service.DTOs;
// using Ledger_and_Inventory_Service.Services.Interfaces;
// using Microsoft.EntityFrameworkCore;
// using Ledger_and_Inventory_Service.Entities;
// using Ledger_and_Inventory_Service.Enums;
// using Ledger_and_Inventory_Service.Errors;
//
// namespace Ledger_and_Inventory_Service.Services;
//
// public class HoldService(AppDbContext db,ILogger<HoldService> logger): IHoldService
// {
//     public async Task<HoldResponse> CreateHoldAsync(
//         CreateHoldRequest request,
//         string correlationId,
//         string? idempotencyKey,
//         CancellationToken ct
//         )
//     {
//             //بررسی تکراری نبودن درخواست هولد
//             var existingHold = await db.Holds
//             .FirstOrDefaultAsync(h => h.ReferenceId == request.TransferId && h.ReferenceType == "TRANSFER", ct); 
//             
//             if (existingHold != null)
//             {
//                 if (existingHold.AccountId != request.AccountId || existingHold.Amount != request.Amount.Amount)
//                 {
//                     throw new AppException("CONFLICT", 409, "Conflict with existing transfer_id hold", "Hold with this transfer_id exists with different parameters.");
//                 }
//                 return MapToResponse(existingHold);
//             }
//             
// //  بررسی وجود حساب و وضعیت posting_allowed             
//             var account = await db.LedgerAccounts
//                 .FirstOrDefaultAsync(a => a.Id == request.AccountId, ct);
//
//             if (account == null)
//             {
//                 throw new AppException("NOT_FOUND", 404, "Account not found", $"Ledger account {request.AccountId} does not exist.");
//             }
//
//             if (!account.PostingAllowed)
//             {
//                 throw new AppException("ACCOUNT_BLOCKED", 422, "Account is blocked", "Posting is not allowed on this account.");
//             }
//             
//             //بررسی موجودی
//             var accountBalance = await db.AccountBalances
//                 .FirstOrDefaultAsync(b => b.AccountId == request.AccountId, ct);
//
//             long bookBalance = accountBalance?.BookBalance ?? 0;
//             long heldAmount = accountBalance?.HeldAmount ?? 0;
//             long availableBalance = bookBalance - heldAmount;
//             if (availableBalance < request.Amount.Amount)
//             {
//                 throw new AppException("INSUFFICIENT_FUNDS", 422, "Insufficient funds", "Available balance is lower than the requested amount.");
//             }
//             
//             //تعیین تاریخ انقضا پیشفرض
//             int expiresInSeconds = request.ExpiresInSeconds ?? 900;
//             var expiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds);
//
//             var hold = new Hold
//             {
//                 Id = Guid.CreateVersion7(),
//                 AccountId = request.AccountId,
//                 Amount = request.Amount.Amount,
//                 Currency = request.Amount.Currency,
//                 Status = HoldStatus.Active,
//                 ReferenceType = "TRANSFER",
//                 ReferenceId = request.TransferId,
//                 ExpiresAt = expiresAt,
//                 CreatedAt = DateTimeOffset.UtcNow,
//                 CorrelationId = correlationId
//             };
//             
//             db.Holds.Add(hold);
//             await db.SaveChangesAsync(ct);
//
//             logger.LogInformation("Hold {HoldId} created for account {AccountId} with amount {Amount}", hold.Id, hold.AccountId, hold.Amount);
//
//             return MapToResponse(hold);
//             
//     }
//
//
//     public async Task<HoldResponse> GetHoldByIdAsync(Guid holdId, CancellationToken ct)
//     {
//         var hold = await db.Holds.FirstOrDefaultAsync(h => h.Id == holdId, ct);
//         if (hold == null)
//         {
//             throw new AppException("NOT_FOUND", 404, "Hold not found", $"Hold with id {holdId} does not exist.");
//         }
//
//         return MapToResponse(hold);
//     }
//
//     private static HoldResponse MapToResponse(Hold hold) => new(
//         hold.Id,
//         hold.AccountId,
//         new MoneyDto(hold.Amount, hold.Currency),
//         hold.Status.ToString().ToUpperInvariant(),
//         hold.ReferenceId,
//         hold.ExpiresAt,
//         hold.CapturedEntryId,
//         hold.CreatedAt,
//         hold.ResolvedAt
//     );
// }