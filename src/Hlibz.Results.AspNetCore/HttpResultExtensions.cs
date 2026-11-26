using Hlibz.Results.AspNetCore.Internal;

using Microsoft.AspNetCore.Http.HttpResults;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// Turns results into minimal API responses: the success status code with the value (or empty)
/// on success, otherwise 400/404/401/403/409 with an <see cref="ErrorResponse"/>, or a
/// <see cref="ResultEnvelope"/> in both cases with the <c>ToEnvelope…</c> variants.
/// </summary>
/// <remarks>
/// The success status code is <c>200</c> unless configured: the endpoint's (or its group's)
/// <see cref="SuccessStatusCodeAttribute"/>, then
/// <see cref="ResultHttpOptions.SuccessStatusCodes"/> for the request's method, then
/// <see cref="ResultHttpOptions.StatusCodes"/>' <see cref="ResultStatus.Success"/>. A
/// <c>204</c> is sent without a body. The <c>ToCreated…</c> variants always send <c>201</c>
/// with a <c>Location</c>.
/// </remarks>
public static class HttpResultExtensions
{
    /// <summary>
    /// Converts a result into a minimal API response: the success status code with
    /// <see cref="Result{T}.Value"/> on success, an <see cref="ErrorResponse"/> otherwise.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The response.</returns>
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredHttpResult(
            context => result.IsSuccess
                ? Success(
                    result.Value,
                    context.SuccessStatusCode,
                    context.CreatedLocation?.LocationFor(result.Value),
                    context.Json)
                : Error(result, context));
    }

    /// <summary>
    /// Converts a result into a minimal API response: the success status code with no body on
    /// success, an <see cref="ErrorResponse"/> otherwise.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The response.</returns>
    public static IResult ToHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredHttpResult(
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
    /// Converts a result into a minimal API response: <c>201 Created</c> with
    /// <see cref="Result{T}.Value"/> and a <c>Location</c> made from it on success, an
    /// <see cref="ErrorResponse"/> otherwise.
    /// </summary>
    /// <example>
    /// <code>
    /// orders.MapPost("", (OrderService service) =&gt;
    ///     service.Create().ToCreatedHttpResult(order =&gt; $"/api/orders/{order.Id}"));
    /// </code>
    /// </example>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="location">Makes the <c>Location</c> from the value; <see langword="null"/>
    /// leaves the header out.</param>
    /// <returns>The response.</returns>
    public static IResult ToCreatedHttpResult<T>(this Result<T> result, Func<T, string?> location)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);
        return new DeferredHttpResult(
            context => result.IsSuccess
                ? Success(
                    result.Value,
                    StatusCodes.Status201Created,
                    location(result.Value),
                    context.Json)
                : Error(result, context));
    }

    /// <summary>
    /// Converts a result into a minimal API response wrapped in a
    /// <see cref="ResultEnvelope{T}"/>, success included, with the status code
    /// <see cref="ToHttpResult{T}"/> would send.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The response.</returns>
    /// <exception cref="InvalidOperationException">On success, when the success status code is
    /// <c>204</c>, which can't carry the envelope.</exception>
    public static IResult ToEnvelopeHttpResult<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredHttpResult(
            context => Envelope(
                result,
                context.SuccessStatusCode,
                result.IsSuccess ? context.CreatedLocation?.LocationFor(result.Value) : null,
                context));
    }

    /// <summary>
    /// Converts a result into a minimal API response wrapped in a <see cref="ResultEnvelope"/>,
    /// success included, with the status code <see cref="ToHttpResult(Result)"/> would send.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The response.</returns>
    /// <exception cref="InvalidOperationException">On success, when the success status code is
    /// <c>204</c>, which can't carry the envelope.</exception>
    public static IResult ToEnvelopeHttpResult(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return new DeferredHttpResult(
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
    /// Converts a result into a minimal API response wrapped in a
    /// <see cref="ResultEnvelope{T}"/>, success included: <c>201 Created</c> with a
    /// <c>Location</c> made from the value on success, the status code
    /// <see cref="ToHttpResult{T}"/> would send otherwise.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="location">Makes the <c>Location</c> from the value; <see langword="null"/>
    /// leaves the header out.</param>
    /// <returns>The response.</returns>
    public static IResult ToCreatedEnvelopeHttpResult<T>(
        this Result<T> result,
        Func<T, string?> location)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(location);
        return new DeferredHttpResult(
            context => Envelope(
                result,
                StatusCodes.Status201Created,
                result.IsSuccess ? location(result.Value) : null,
                context));
    }

    private static IResult Success<T>(
        T? value,
        int statusCode,
        string? location,
        JsonSerializerOptions json)
    {
        IResult response = statusCode switch
        {
            // Result<Unit, TError>: an operation that returns nothing, sent as a Result is.
            _ when typeof(T) == typeof(Unit) => Success(statusCode),
            StatusCodes.Status200OK => TypedResults.Ok(value),
            StatusCodes.Status204NoContent => TypedResults.NoContent(),
            _ => TypedResults.Json(value, ResultResponses.TypeInfo<T>(json), statusCode: statusCode),
        };
        return WithLocation(response, location);
    }

    private static IResult Success(int statusCode) =>
        statusCode switch
        {
            StatusCodes.Status200OK => TypedResults.Ok(),
            StatusCodes.Status204NoContent => TypedResults.NoContent(),
            _ => TypedResults.StatusCode(statusCode),
        };

    private static IResult Envelope<T>(
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

    private static IResult Envelope(
        object envelope,
        int statusCode,
        string? location,
        ResultResponseContext context) =>
        WithLocation(
            Microsoft.AspNetCore.Http.Results.Json(
                envelope,
                context.Json.GetTypeInfo(envelope.GetType()),
                statusCode: ResultResponses.EnvelopeHttpStatusCode(statusCode, context.Options)),
            location);

    private static IResult WithLocation(IResult response, string? location) =>
        location is null ? response : new LocationHttpResult(response, location);

    private static JsonHttpResult<ErrorResponse> Error(Result result, ResultResponseContext context) =>
        TypedResults.Json(
            ResultResponses.ToErrorResponse(result.Error!, context.Options, context.Json),
            ResultResponses.TypeInfo<ErrorResponse>(context.Json),
            statusCode: ResultResponses.StatusCodeFor(result.Status, context.Options));
}
