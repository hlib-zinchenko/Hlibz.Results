using Hlibz.Results;

namespace Shop.Core.Payments;

/// <summary>
/// A second module in the same assembly: its codes land in the same catalog as the orders ones.
/// </summary>
public sealed record PaymentsErrorCode(string Code, string Message)
    : ErrorCode($"payments_{Code}", Message);

public static class PaymentsErrorCodes
{
    public static readonly ErrorCode ProviderDown = new PaymentsErrorCode(
        "1001",
        "The payment provider is temporarily unavailable. Please retry in a minute.");
}
