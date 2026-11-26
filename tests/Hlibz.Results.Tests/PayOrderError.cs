namespace Hlibz.Results.Tests;

/// <summary>
/// An error set. A union compiles to a struct with no equality of its own, which CA1815 flags;
/// it answers "which error is it?", not "are these equal?".
/// </summary>
#pragma warning disable CA1815
public union PayOrderError(OrderNotFound, OrderClosed);
#pragma warning restore CA1815
