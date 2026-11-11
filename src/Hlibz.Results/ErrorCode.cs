namespace Hlibz.Results;

/// <summary>
/// A machine-readable error code with the message that goes with it. Declare them as
/// <see langword="public static readonly"/> fields so <see cref="ErrorCodeCatalog"/> can find
/// them; derive a record per module to give its codes a common prefix.
/// </summary>
/// <param name="Code">The code clients branch on, e.g. <c>orders_1001</c>. Unique per catalog.</param>
/// <param name="Message">A human-readable description of the failure.</param>
/// <example>
/// <code>
/// public sealed record OrdersErrorCode(string Code, string Message)
///     : ErrorCode($"orders_{Code}", Message);
///
/// public static class OrdersErrorCodes
/// {
///     public static readonly ErrorCode OrderClosed = new OrdersErrorCode("1001", "The order is closed.");
/// }
/// </code>
/// </example>
public record ErrorCode(string Code, string Message);
