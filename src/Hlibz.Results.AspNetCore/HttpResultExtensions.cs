using Hlibz.Results.AspNetCore.Internal;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// Turns results into minimal API responses: 200 with the value (or empty) on success,
/// otherwise 400/404/401/403/409 with an <see cref="ErrorResponse"/>, or a
/// <see cref="ResultEnvelope"/> in both cases with the <c>ToEnvelope…</c> variants.
/// </summary>
public static class HttpResultExtensions
{
    /// <summary>
    /// Converts a result into a minimal API response: <c>200</c> with
    /// <see cref="Result{T}.Value"/> on success, an <see cref="ErrorResponse"/> otherwise.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The response.</returns>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredHttpResult(
            (options, json) => result.IsSuccess
                ? TypedResults.Ok(result.Value)
                : Error(result, options, json));
    }

    /// <summary>
    /// Converts a result into a minimal API response: an empty <c>200</c> on success, an
    /// <see cref="ErrorResponse"/> otherwise.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The response.</returns>
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredHttpResult(
            (options, json) => result.IsSuccess ? TypedResults.Ok() : Error(result, options, json));
    }

    /// <summary>
    /// Converts a result into a minimal API response wrapped in a
    /// <see cref="ResultEnvelope{T}"/>, success included, with the status code
    /// <see cref="ToHttpResult{T}"/> would send.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The response.</returns>
    public static IResult ToEnvelopeHttpResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredHttpResult(
            (options, json) => Envelope(
                ResultResponses.ToEnvelope(result, options, json),
                result,
                options,
                json));
    }

    /// <summary>
    /// Converts a result into a minimal API response wrapped in a <see cref="ResultEnvelope"/>,
    /// success included, with the status code <see cref="ToHttpResult(Result)"/> would send.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The response.</returns>
    public static IResult ToEnvelopeHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredHttpResult(
            (options, json) => Envelope(
                ResultResponses.ToEnvelope(result, options, json),
                result,
                options,
                json));
    }

    private static JsonHttpResult<object> Envelope(
        object envelope,
        Result result,
        ResultHttpOptions options,
        JsonSerializerOptions json) =>
        TypedResults.Json(
            envelope,
            json,
            statusCode: ResultResponses.EnvelopeHttpStatusCode(
                ResultResponses.StatusCodeFor(result.Status, options),
                options));

    private static JsonHttpResult<ErrorResponse> Error(
        Result result,
        ResultHttpOptions options,
        JsonSerializerOptions json) =>
        TypedResults.Json(
            ResultResponses.ToErrorResponse(result, options, json),
            json,
            statusCode: ResultResponses.StatusCodeFor(result.Status, options));
}
