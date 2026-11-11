using Hlibz.Results;
using Hlibz.Results.AspNetCore;

using Microsoft.AspNetCore.Mvc;

using Shop.Core.Orders;

namespace Shop.Controllers.Controllers;

/// <summary>
/// The same use cases as the minimal API sample, through controllers and the envelope: every
/// body, success included, is <c>{ data, is_success, status, message, code, validation_errors }</c>.
/// </summary>
[ApiController]
[Route("api/orders")]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    private const string UserHeader = "X-User";

    [HttpPost]
    public ActionResult<ResultEnvelope<OrderDto>> Create(
        [FromHeader(Name = UserHeader)] string? user) =>
        Customer(user).Bind(orders.Create).ToEnvelopeActionResult();

    [HttpGet("{id:guid}")]
    public ActionResult<ResultEnvelope<OrderDto>> Get(
        Guid id,
        [FromHeader(Name = UserHeader)] string? user) =>
        Customer(user).Bind(customer => orders.Get(id, customer)).ToEnvelopeActionResult();

    [HttpPost("{id:guid}/lines")]
    public ActionResult<ResultEnvelope<OrderDto>> AddLine(
        Guid id,
        AddLineRequest line,
        [FromHeader(Name = UserHeader)] string? user) =>
        Customer(user)
            .Bind(customer => orders.AddLine(id, customer, line))
            .ToEnvelopeActionResult();

    [HttpPost("{id:guid}/pay")]
    public async Task<ActionResult<ResultEnvelope<OrderDto>>> Pay(
        Guid id,
        [FromHeader(Name = UserHeader)] string? user,
        CancellationToken cancellationToken)
    {
        Result<string> customer = Customer(user);
        if (customer.IsFailure)
        {
            return Result<OrderDto>.FromFailed(customer).ToEnvelopeActionResult();
        }

        Result<OrderDto> paid = await orders.PayAsync(id, customer.Value, cancellationToken);
        return paid.ToEnvelopeActionResult();
    }

    /// <summary>
    /// No authentication middleware here: a missing <c>X-User</c> header is just another failed
    /// result, <see cref="ResultStatus.Unauthorized"/> (401), in the same envelope.
    /// </summary>
    private static Result<string> Customer(string? user) =>
        string.IsNullOrWhiteSpace(user)
            ? Result<string>.Unauthorized("Send an X-User header to say who you are.")
            : Result<string>.Success(user);
}
