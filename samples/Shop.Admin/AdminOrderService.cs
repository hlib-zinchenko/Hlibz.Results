using Hlibz.Results;

using Shop.Core.Orders;
using Shop.Core.Payments;

namespace Shop.Admin;

public sealed record RefundRequest(decimal Amount);

public sealed record PaymentProviderStatus(bool IsDown);

public sealed class AdminOrderService(OrderStore store, PaymentGateway payments)
{
    public Result<IReadOnlyList<OrderDto>> GetAll() =>
        Result<IReadOnlyList<OrderDto>>.Success([.. store.Orders.Values.Select(OrderDto.From)]);

    public Result<OrderDto> Refund(Guid orderId, RefundRequest request)
    {
        if (request.Amount <= 0)
        {
            // Invalid without FluentValidation: build the errors by hand.
            return Result<OrderDto>.Invalid(new Dictionary<string, string[]>
            {
                [nameof(RefundRequest.Amount)] = ["The amount must be positive."],
            });
        }

        if (!store.Orders.TryGetValue(orderId, out Order? order))
        {
            return Result<OrderDto>.NotFound("Order not found.");
        }

        if (!order.IsPaid)
        {
            return Result<OrderDto>.Conflict(AdminErrorCodes.OrderNotPaid);
        }

        if (order.RefundedAmount + request.Amount > order.Total)
        {
            return Result<OrderDto>.Error(AdminErrorCodes.RefundTooLarge);
        }

        order.Refund(request.Amount);
        return Result<OrderDto>.Success(OrderDto.From(order));
    }

    public Result<PaymentProviderStatus> SetPaymentProvider(PaymentProviderStatus status)
    {
        payments.IsDown = status.IsDown;
        return Result<PaymentProviderStatus>.Success(status);
    }
}
