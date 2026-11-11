using Hlibz.Results;

namespace Shop.Core.Payments;

/// <summary>
/// A fake payment provider an admin can take down, to show <see cref="ResultStatus.Unavailable"/>
/// (503): an expected, retryable failure of something the shop depends on, not a bug.
/// </summary>
public sealed class PaymentGateway
{
    public bool IsDown { get; set; }

    public async Task<Result> ChargeAsync(decimal amount, CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(10), cancellationToken);
        return IsDown
            ? Result.Unavailable(PaymentsErrorCodes.ProviderDown)
            : Result.Success();
    }
}
