using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Hlibz.Results.AspNetCore.Tests;

[ApiController]
[Route("mvc")]
public sealed class ResultsController : ControllerBase
{
    [HttpGet("order")]
    public ActionResult<OrderDto> GetOrder() =>
        Result<OrderDto>.Success(new OrderDto(7, "Ann")).ToActionResult();

    [HttpPost("ok")]
    public ActionResult Done() => Result.Success().ToActionResult();

    [HttpGet("missing")]
    public ActionResult<OrderDto> Missing() => Result<OrderDto>.NotFound().ToActionResult();

    [HttpGet("invalid")]
    public ActionResult<OrderDto> Invalid() =>
        Result<OrderDto>.Invalid(
                new Dictionary<string, string[]> { ["Lines[1].UnitPrice"] = ["Too low."] })
            .ToActionResult();

    [HttpGet("invalid-422")]
    public ActionResult Invalid422() =>
        Result.Invalid(new Dictionary<string, string[]> { ["Name"] = ["Required."] })
            .ToActionResult();

    [HttpGet("envelope")]
    public ActionResult<ResultEnvelope<OrderDto>> Envelope() =>
        Result<OrderDto>.Success(new OrderDto(7, "Ann")).ToEnvelopeActionResult();

    [HttpGet("envelope-conflict")]
    public ActionResult<ResultEnvelope> EnvelopeConflict() =>
        Result.Conflict(TestErrorCodes.Duplicate).ToEnvelopeActionResult();

    [HttpPost("accepted")]
    [SuccessStatusCode(StatusCodes.Status202Accepted)]
    public ActionResult<OrderDto> AcceptOrder() =>
        Result<OrderDto>.Success(new OrderDto(7, "Ann")).ToActionResult();

    [HttpDelete("order")]
    public ActionResult DeleteOrder() => Result.Success().ToActionResult();

    [HttpPost("created")]
    public ActionResult<OrderDto> CreateOrder() =>
        Result<OrderDto>.Success(new OrderDto(7, "Ann"))
            .ToCreatedActionResult(order => $"/mvc/order/{order.OrderId}");

    [HttpPost("created-envelope")]
    public ActionResult<ResultEnvelope<OrderDto>> CreatedEnvelope() =>
        Result<OrderDto>.Success(new OrderDto(7, "Ann"))
            .ToCreatedEnvelopeActionResult(order => $"/mvc/order/{order.OrderId}");
}

[ApiController]
[Route("mvc/imports")]
[SuccessStatusCode(StatusCodes.Status202Accepted)]
public sealed class ImportsController : ControllerBase
{
    [HttpPost]
    public ActionResult<OrderDto> Start() =>
        Result<OrderDto>.Success(new OrderDto(7, "Ann")).ToActionResult();

    [HttpPost("now")]
    [SuccessStatusCode(StatusCodes.Status201Created)]
    public ActionResult<OrderDto> Now() =>
        Result<OrderDto>.Success(new OrderDto(7, "Ann")).ToActionResult();
}
