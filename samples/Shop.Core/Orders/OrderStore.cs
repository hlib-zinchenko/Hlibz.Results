using System.Collections.Concurrent;

namespace Shop.Core.Orders;

/// <summary>
/// In-memory storage shared by the customer and admin services; a real app has a database here.
/// </summary>
public sealed class OrderStore
{
    public ConcurrentDictionary<Guid, Order> Orders { get; } = new();

    public IReadOnlyDictionary<string, Product> Products { get; } =
        new Dictionary<string, Product>(StringComparer.OrdinalIgnoreCase)
        {
            ["MUG"] = new("MUG", 12.50m, 10),
            ["TEE"] = new("TEE", 25m, 3),
        };
}
