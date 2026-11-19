using FluentValidation;
using Ledger_and_Inventory_Service.DTOs;

namespace Ledger_and_Inventory_Service.Validators;

public class UpdateAccountPermissionValidator : AbstractValidator<UpdateAccountPermissionRequest>
{
    public UpdateAccountPermissionValidator()
    {
        RuleFor(x => x.Allowed)
            .NotNull().WithMessage("allowed is required.");

        // reason is optional (agreed with S1)
        RuleFor(x => x.Reason)
            .MaximumLength(200).WithMessage("reason cannot exceed 200 characters.");
    }
}