using FluentValidation;
using Ledger_and_Inventory_Service.DTOs;

namespace Ledger_and_Inventory_Service.Validators;

public class CreateHoldValidator : AbstractValidator<CreateHoldRequest>
{
    public CreateHoldValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("account_id is required.");

        RuleFor(x => x.TransferId)
            .NotEmpty().WithMessage("transfer_id is required.");

        RuleFor(x => x.Amount)
            .NotNull().WithMessage("amount is required.");

        When(x => x.Amount is not null, () =>
        {
            RuleFor(x => x.Amount.Amount)
                .GreaterThan(0).WithMessage("amount.amount must be greater than zero.");

            RuleFor(x => x.Amount.Currency)
                .Equal("IRR").WithMessage("amount.currency must be IRR.");
        });

        RuleFor(x => x.ExpiresInSeconds)
            .InclusiveBetween(1, 86400)
            .When(x => x.ExpiresInSeconds.HasValue)
            .WithMessage("expires_in_seconds must be between 1 and 86400.");

        RuleFor(x => x.Description)
            .MaximumLength(300).WithMessage("description cannot exceed 300 characters.");
    }
}