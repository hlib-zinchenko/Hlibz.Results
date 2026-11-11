using Hlibz.Results;

namespace Shop.Admin;

/// <summary>
/// Admin-only codes. They live in their own assembly, so the customer app's catalog, built from
/// Shop.Core alone, never lists them.
/// </summary>
public sealed record AdminErrorCode(string Code, string Message)
    : ErrorCode($"admin_{Code}", Message);

public static class AdminErrorCodes
{
    public static readonly ErrorCode OrderNotPaid =
        new AdminErrorCode("1001", "Only a paid order can be refunded.");

    public static readonly ErrorCode RefundTooLarge =
        new AdminErrorCode("1002", "The refund would exceed what the customer paid.");
}
