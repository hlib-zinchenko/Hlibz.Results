namespace Shop.Core.Orders;

public sealed record OrderDto(
    Guid Id,
    string CustomerId,
    IReadOnlyList<OrderLine> Lines,
    decimal Total,
    bool IsPaid,
    decimal RefundedAmount)
{
    public static OrderDto From(Order order) => new(
        order.Id,
        order.CustomerId,
        [.. order.Lines],
        order.Total,
        order.IsPaid,
        order.RefundedAmount);
}

public sealed record AddLineRequest(string Sku, int Quantity);
