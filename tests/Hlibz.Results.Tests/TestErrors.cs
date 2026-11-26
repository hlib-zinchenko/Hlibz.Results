namespace Hlibz.Results.Tests;

#pragma warning disable SA1649 // The errors the tests return, together.

[Error("orders_1001", ResultStatus.Conflict, "The order is closed.")]
public sealed record OrderClosed(Guid OrderId) : Error;

[Error("orders_1002", ResultStatus.NotFound, "There's no such order.")]
public sealed record OrderNotFound : Error;

[Error("orders_1003", ResultStatus.Error, "The order is too large.")]
public sealed record OrderTooLarge(int Lines, int MaxLines) : Error
{
    protected override string FormatMessage(string message) =>
        $"The order has {Lines} lines; it can have {MaxLines}.";
}

[Error("orders_1004", ResultStatus.Error, "Not in any set.")]
public sealed record Unlisted : Error;
