using Hlibz.Results;

namespace Shop.Core.Orders;

/// <summary>
/// The orders module's codes, all prefixed <c>orders_</c>, so codes from different modules can't
/// collide and clients can tell where a failure came from.
/// </summary>
public sealed record OrdersErrorCode(string Code, string Message)
    : ErrorCode($"orders_{Code}", Message);

/// <summary>
/// Every code is a public static field, which is how the error code catalog finds it.
/// </summary>
public static class OrdersErrorCodes
{
    public static readonly ErrorCode OrderClosed =
        new OrdersErrorCode("1001", "The order is closed and can't be changed.");

    public static readonly ErrorCode OutOfStock =
        new OrdersErrorCode("1002", "There isn't enough of this product in stock.");

    public static readonly ErrorCode NotYourOrder =
        new OrdersErrorCode("1003", "The order belongs to another customer.");

    public static readonly ErrorCode EmptyOrder =
        new OrdersErrorCode("1004", "An order needs at least one line before it can be paid.");
}
