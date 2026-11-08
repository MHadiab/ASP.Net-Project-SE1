using FluentValidation.Results;
using Ledger_and_Inventory_Service.Errors;

namespace Ledger_and_Inventory_Service.Validators;

public static class ValidationResultExtensions
{
    // Turns a failed FluentValidation result into the standard 400 ERROR_VALIDATION problem.
    public static void ThrowIfInvalid(this ValidationResult result)
    {
        if (result.IsValid) return;

        throw new AppException(
            "ERROR_VALIDATION",
            400,
            "Validation failed",
            string.Join(" ", result.Errors.Select(e => e.ErrorMessage)));
    }
}