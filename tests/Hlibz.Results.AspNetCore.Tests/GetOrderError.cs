namespace Hlibz.Results.AspNetCore.Tests;

#pragma warning disable CA1815 // A union answers "which error is it?", not "are these equal?".
public union GetOrderError(OrderNotFound, OrderClosed, Taken);
