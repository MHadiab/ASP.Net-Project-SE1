using FluentValidation;
using Ledger_and_Inventory_Service.DTOs;

namespace Ledger_and_Inventory_Service.Validators;

public class CreateLedgerAccountValidator : AbstractValidator<CreateLedgerAccountRequest>
{
    public CreateLedgerAccountValidator()
    {
        RuleFor(x => x.AccountId)
            .NotEmpty().WithMessage("account_id is required.");

        // SYSTEM accounts are seeded at startup and cannot be created through the API.
        RuleFor(x => x.AccountType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("account_type is required.")
            .Equal("CUSTOMER").WithMessage("account_type must be CUSTOMER.");

        RuleFor(x => x.Currency)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("currency is required.")
            .Equal("IRR").WithMessage("currency must be IRR.");
    }
}