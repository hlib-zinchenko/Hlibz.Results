using Hlibz.Results.AspNetCore.Internal;

using Microsoft.AspNetCore.Mvc;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// Turns results into controller action results with the same status codes and bodies as
/// <see cref="HttpResultExtensions"/>.
/// </summary>
/// <remarks>
/// The success status code is <c>200</c> unless configured: the action's (or its controller's)
/// <see cref="SuccessStatusCodeAttribute"/>, then
/// <see cref="ResultHttpOptions.SuccessStatusCodes"/> for the request's method, then
/// <see cref="ResultHttpOptions.StatusCodes"/>' <see cref="ResultStatus.Success"/>. A
/// <c>204</c> is sent without a body. The <c>ToCreated…</c> variants always send <c>201</c>
/// with a <c>Location</c>.
/// </remarks>
public static class ActionResultExtensions
{
    /// <summary>
    /// Converts a result into an action result: the success status code with
    /// <see cref="Result{T}.Value"/> on success, an <see cref="ErrorResponse"/> otherwise.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The action result.</returns>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredActionResult(
            context => result.IsSuccess
                ? Success(
                    result.Value,
                    context.SuccessStatusCode,
                    context.CreatedLocation?.LocationFor(result.Value))
                : Error(result, context));
    }

    /// <summary>
    /// Converts a result into an action result: the success status code with no body on
    /// success, an <see cref="ErrorResponse"/> otherwise.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The action result.</returns>
    public static ActionResult ToActionResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredActionResult(
            context =>
            {
                if (result.IsFailure)
                {
                    return Error(result, context);
                }

                context.RequireNoCreatedLocation();
                return Success(context.SuccessStatusCode);
            });
    }

    /// <summary>
    /// Converts a result into an action result: <c>201 Created</c> with
    /// <see cref="Result{T}.Value"/> and a <c>Location</c> made from it on success, an
    /// <see cref="ErrorResponse"/> otherwise.
    /// </summary>
    /// <example>
    /// <code>
    /// [HttpPost]
    /// public ActionResult&lt;OrderDto&gt; Create() =&gt;
    ///     orders.Create().ToCreatedActionResult(order =&gt; $"/api/orders/{order.Id}");
    /// </code>
    /// </example>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="location">Makes the <c>Location</c> from the value; <see langword="null"/>
    /// leaves the header out.</param>
    /// <returns>The action result.</returns>
    public static ActionResult<T> ToCreatedActionResult<T>(
        this Result<T> result,
        Func<T, string?> location)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);
        return new DeferredActionResult(
            context => result.IsSuccess
                ? Success(result.Value, StatusCodes.Status201Created, location(result.Value))
                : Error(result, context));
    }

    /// <summary>
    /// Converts a result into an action result wrapped in a <see cref="ResultEnvelope{T}"/>,
    /// success included, with the status code <see cref="ToActionResult{T}"/> would send.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The action result.</returns>
    /// <exception cref="InvalidOperationException">On success, when the success status code is
    /// <c>204</c>, which can't carry the envelope.</exception>
    public static ActionResult<ResultEnvelope<T>> ToEnvelopeActionResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredActionResult(
            context => Envelope(
                result,
                context.SuccessStatusCode,
                result.IsSuccess ? context.CreatedLocation?.LocationFor(result.Value) : null,
                context));
    }

    /// <summary>
    /// Converts a result into an action result wrapped in a <see cref="ResultEnvelope"/>,
    /// success included, with the status code <see cref="ToActionResult(Result)"/> would send.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The action result.</returns>
    /// <exception cref="InvalidOperationException">On success, when the success status code is
    /// <c>204</c>, which can't carry the envelope.</exception>
    public static ActionResult<ResultEnvelope> ToEnvelopeActionResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredActionResult(
            context =>
            {
                if (result.IsSuccess)
                {
                    context.RequireNoCreatedLocation();
                }

                int statusCode = ResultResponses.EnvelopeStatusCodeFor(
                    result,
                    context.SuccessStatusCode,
                    context.Options);
                return Envelope(
                    ResultResponses.ToEnvelope(result, statusCode, context.Options, context.Json),
                    statusCode,
                    location: null,
                    context);
            });
    }

    /// <summary>
    /// Converts a result into an action result wrapped in a <see cref="ResultEnvelope{T}"/>,
    /// success included: <c>201 Created</c> with a <c>Location</c> made from the value on
    /// success, the status code <see cref="ToActionResult{T}"/> would send otherwise.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="location">Makes the <c>Location</c> from the value; <see langword="null"/>
    /// leaves the header out.</param>
    /// <returns>The action result.</returns>
    public static ActionResult<ResultEnvelope<T>> ToCreatedEnvelopeActionResult<T>(
        this Result<T> result,
        Func<T, string?> location)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);
        return new DeferredActionResult(
            context => Envelope(
                result,
                StatusCodes.Status201Created,
                result.IsSuccess ? location(result.Value) : null,
                context));
    }

    private static IActionResult Success(object? value, int statusCode, string? location)
    {
        IActionResult response = statusCode switch
        {
            StatusCodes.Status200OK => new OkObjectResult(value),
            StatusCodes.Status204NoContent => new NoContentResult(),
            _ => new ObjectResult(value) { StatusCode = statusCode },
        };
        return WithLocation(response, location);
    }

    private static StatusCodeResult Success(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status200OK => new OkResult(),
            StatusCodes.Status204NoContent => new NoContentResult(),
            _ => new StatusCodeResult(statusCode),
        };

    private static IActionResult Envelope<T>(
        Result<T> result,
        int successStatusCode,
        string? location,
        ResultResponseContext context)
    {
        int statusCode = ResultResponses.EnvelopeStatusCodeFor(
            result,
            successStatusCode,
            context.Options);
        return Envelope(
            ResultResponses.ToEnvelope(result, statusCode, context.Options, context.Json),
            statusCode,
            location,
            context);
    }

    private static IActionResult Envelope(
        object envelope,
        int statusCode,
        string? location,
        ResultResponseContext context) =>
        WithLocation(
            new ObjectResult(envelope)
            {
                StatusCode = ResultResponses.EnvelopeHttpStatusCode(statusCode, context.Options),
            },
            location);

    private static IActionResult WithLocation(IActionResult response, string? location) =>
        location is null ? response : new LocationActionResult(response, location);

    private static ObjectResult Error(Result result, ResultResponseContext context) =>
        new(ResultResponses.ToErrorResponse(result, context.Options, context.Json))
        {
            StatusCode = ResultResponses.StatusCodeFor(result.Status, context.Options),
        };
}
