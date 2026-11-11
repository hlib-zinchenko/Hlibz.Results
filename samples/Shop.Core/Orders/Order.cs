using Hlibz.Results;

namespace Shop.Core.Orders;

/// <summary>
/// The entity guards its own rules and says no with a <see cref="Result"/> instead of throwing.
/// </summary>
public sealed class Order(Guid id, string customerId)
{
    private readonly List<OrderLine> _lines = [];

    public Guid Id { get; } = id;

    public string CustomerId { get; } = customerId;

    public IReadOnlyList<OrderLine> Lines => _lines;

    public bool IsPaid { get; private set; }

    public decimal RefundedAmount { get; private set; }

    public decimal Total => _lines.Sum(line => line.UnitPrice * line.Quantity);

    public Result AddLine(Product product, int quantity)
    {
        if (IsPaid)
        {
            return Result.Conflict(OrdersErrorCodes.OrderClosed);
        }

        if (product.Stock < quantity)
        {
            return Result.Error(OrdersErrorCodes.OutOfStock);
        }

        product.Stock -= quantity;
        _lines.Add(new OrderLine(product.Sku, product.Price, quantity));
        return Result.Success();
    }

    public Result EnsurePayable()
    {
        if (IsPaid)
        {
            return Result.Conflict(OrdersErrorCodes.OrderClosed);
        }

        return _lines.Count == 0 ? Result.Error(OrdersErrorCodes.EmptyOrder) : Result.Success();
    }

    public void MarkPaid() => IsPaid = true;

    public void Refund(decimal amount) => RefundedAmount += amount;
}

public sealed record OrderLine(string Sku, decimal UnitPrice, int Quantity);

public sealed class Product(string sku, decimal price, int stock)
{
    public string Sku { get; } = sku;

    public decimal Price { get; } = price;

    public int Stock { get; set; } = stock;
}
