using Hlibz.Results.AspNetCore.Internal;

using Microsoft.AspNetCore.Mvc;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// Turns results into controller action results with the same status codes and bodies as
/// <see cref="HttpResultExtensions"/>.
/// </summary>
public static class ActionResultExtensions
{
    /// <summary>
    /// Converts a result into an action result: <c>200</c> with <see cref="Result{T}.Value"/> on
    /// success, an <see cref="ErrorResponse"/> otherwise.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The action result.</returns>
    public static ActionResult<T> ToActionResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredActionResult(
            (options, json) => result.IsSuccess
                ? new OkObjectResult(result.Value)
                : Error(result, options, json));
    }

    /// <summary>
    /// Converts a result into an action result: an empty <c>200</c> on success, an
    /// <see cref="ErrorResponse"/> otherwise.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The action result.</returns>
    public static ActionResult ToActionResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredActionResult(
            (options, json) => result.IsSuccess ? new OkResult() : Error(result, options, json));
    }

    /// <summary>
    /// Converts a result into an action result wrapped in a <see cref="ResultEnvelope{T}"/>,
    /// success included, with the status code <see cref="ToActionResult{T}"/> would send.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The action result.</returns>
    public static ActionResult<ResultEnvelope<T>> ToEnvelopeActionResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredActionResult(
            (options, json) => new ObjectResult(ResultResponses.ToEnvelope(result, options, json))
            {
                StatusCode = ResultResponses.EnvelopeHttpStatusCode(
                    ResultResponses.StatusCodeFor(result.Status, options),
                    options),
            });
    }

    /// <summary>
    /// Converts a result into an action result wrapped in a <see cref="ResultEnvelope"/>,
    /// success included, with the status code <see cref="ToActionResult(Result)"/> would send.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The action result.</returns>
    public static ActionResult<ResultEnvelope> ToEnvelopeActionResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredActionResult(
            (options, json) => new ObjectResult(ResultResponses.ToEnvelope(result, options, json))
            {
                StatusCode = ResultResponses.EnvelopeHttpStatusCode(
                    ResultResponses.StatusCodeFor(result.Status, options),
                    options),
            });
    }

    private static ObjectResult Error(
        Result result,
        ResultHttpOptions options,
        JsonSerializerOptions json) =>
        new(ResultResponses.ToErrorResponse(result, options, json))
        {
            StatusCode = ResultResponses.StatusCodeFor(result.Status, options),
        };
}
