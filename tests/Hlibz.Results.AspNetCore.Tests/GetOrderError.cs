namespace Hlibz.Results.AspNetCore.Tests;

public union GetOrderError(OrderNotFound, OrderClosed, Taken);

public union SendOrderError(Duplicate, AlreadySent);

// Not an error set, so Hlibz.Results doesn't suppress CA1815 on it.
#pragma warning disable CA1815 // A union is a struct without equality members.
public union NotAnErrorSet(int, string);
#pragma warning restore CA1815
