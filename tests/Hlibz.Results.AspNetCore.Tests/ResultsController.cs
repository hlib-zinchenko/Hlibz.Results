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
}
