using FluentValidation;
using FluentValidation.Results;

using Hlibz.Results;
using Hlibz.Results.FluentValidation;

using Shop.Core.Payments;

namespace Shop.Core.Orders;

/// <summary>
/// The customer-facing use cases. Every method returns a <see cref="Result"/>: the API layer turns
/// it into a response with one call and never decides status codes itself.
/// </summary>
public sealed class OrderService(
    OrderStore store,
    PaymentGateway payments,
    IValidator<AddLineRequest> lineValidator)
{
    public Result<OrderDto> Create(string customerId)
    {
        Order order = new(Guid.NewGuid(), customerId);
        store.Orders[order.Id] = order;
        return Result<OrderDto>.Success(OrderDto.From(order));
    }

    public Result<OrderDto> Get(Guid orderId, string customerId) =>
        Find(orderId, customerId).Map(OrderDto.From);

    public Result<OrderDto> AddLine(Guid orderId, string customerId, AddLineRequest request)
    {
        // A failed validation becomes an Invalid failure with its errors (400, or 422 if the app
        // maps it so), keyed by property; the HTTP layer cases the keys like the JSON. An API that
        // doesn't tell validation apart would use validation.ToErrorResult<OrderDto>() instead.
        ValidationResult validation = lineValidator.Validate(request);
        if (!validation.IsValid)
        {
            return validation.ToInvalidResult<OrderDto>();
        }

        if (!store.Products.TryGetValue(request.Sku, out Product? product))
        {
            return Result<OrderDto>.NotFound($"There is no product '{request.Sku}'.");
        }

        // Bind runs the next step only on success; any failure is passed on unchanged.
        return Find(orderId, customerId).Bind(order =>
        {
            Result added = order.AddLine(product, request.Quantity);
            return added.IsSuccess
                ? Result<OrderDto>.Success(OrderDto.From(order))
                : Result<OrderDto>.FromFailed(added);
        });
    }

    public async Task<Result<OrderDto>> PayAsync(
        Guid orderId,
        string customerId,
        CancellationToken cancellationToken)
    {
        Result<Order> found = Find(orderId, customerId);
        if (found.IsFailure)
        {
            return Result<OrderDto>.FromFailed(found);
        }

        Order order = found.Value;
        Result payable = order.EnsurePayable();
        if (payable.IsFailure)
        {
            return Result<OrderDto>.FromFailed(payable);
        }

        Result charged = await payments.ChargeAsync(order.Total, cancellationToken);
        if (charged.IsFailure)
        {
            return Result<OrderDto>.FromFailed(charged);
        }

        order.MarkPaid();
        return Result<OrderDto>.Success(OrderDto.From(order));
    }

    private Result<Order> Find(Guid orderId, string customerId)
    {
        if (!store.Orders.TryGetValue(orderId, out Order? order))
        {
            return Result<Order>.NotFound("Order not found.");
        }

        return order.CustomerId == customerId
            ? Result<Order>.Success(order)
            : Result<Order>.Forbidden(OrdersErrorCodes.NotYourOrder);
    }
}
