using FluentValidation;

namespace Shop.Core.Orders;

public sealed class AddLineRequestValidator : AbstractValidator<AddLineRequest>
{
    public AddLineRequestValidator()
    {
        RuleFor(request => request.Sku).NotEmpty();
        RuleFor(request => request.Quantity).InclusiveBetween(1, 5);
    }
}
