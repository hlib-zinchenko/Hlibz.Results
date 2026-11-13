using Hlibz.Results.AspNetCore.Internal;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// Writes the API's error body from outside an endpoint (middleware, the rate limiter, exception
/// handlers), with the same JSON settings and shape as the endpoints use.
/// </summary>
public static class HttpResponseErrorExtensions
{
    /// <summary>
    /// Writes <paramref name="error"/> as the response body; an envelope instead when
    /// <see cref="ResultHttpOptions.UseEnvelope"/> is on (the custom one from
    /// <see cref="ResultHttpOptions.CreateEnvelope"/> if set, sent as <c>200</c> under
    /// <see cref="ResultHttpOptions.EnvelopeAlwaysOk"/>).
    /// </summary>
    /// <param name="response">The response to write to; its status code is already set.</param>
    /// <param name="error">The error body.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <returns>A task that completes when the body is written.</returns>
    public static Task WriteErrorAsync(
        this HttpResponse response,
        ErrorResponse error,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(error);

        HttpContext context = response.HttpContext;
        JsonSerializerOptions json = ResultResponses.GetJsonOptions(context);
        ResultHttpOptions options = ResultResponses.GetOptions(context.RequestServices);

        if (!options.UseEnvelope)
        {
            return response.WriteAsJsonAsync(
                error,
                ResultResponses.TypeInfo<ErrorResponse>(json),
                cancellationToken: cancellationToken);
        }

        object envelope = ResultResponses.ToEnvelope(error, response.StatusCode, options);
        response.StatusCode =
            ResultResponses.EnvelopeHttpStatusCode(response.StatusCode, options);
        return response.WriteAsJsonAsync(
            envelope,
            json.GetTypeInfo(envelope.GetType()),
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Writes an error body with <paramref name="message"/> (the default message for the
    /// response's status code when <see langword="null"/>) and <paramref name="code"/>.
    /// </summary>
    /// <param name="response">The response to write to; its status code is already set.</param>
    /// <param name="message">The message, or <see langword="null"/> for the default.</param>
    /// <param name="code">The error code, if any.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <returns>A task that completes when the body is written.</returns>
    public static Task WriteErrorAsync(
        this HttpResponse response,
        string? message,
        string? code = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ResultHttpOptions options = ResultResponses.GetOptions(response.HttpContext.RequestServices);
        ErrorResponse error = new()
        {
            Message = message
                      ?? ResultResponses.DefaultMessageFor(response.StatusCode, options.Messages),
            Code = code,
        };
        return response.WriteErrorAsync(error, cancellationToken);
    }

    /// <summary>
    /// Writes a failed <paramref name="result"/> outside an endpoint: sets the status code and
    /// writes its error body (or envelope).
    /// </summary>
    /// <param name="response">The response to write to.</param>
    /// <param name="result">The failed result.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <returns>A task that completes when the body is written.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="result"/> succeeded.</exception>
    public static Task WriteErrorAsync(
        this HttpResponse response,
        Result result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot write a successful result as an error.");
        }

        HttpContext context = response.HttpContext;
        ResultHttpOptions options = ResultResponses.GetOptions(context.RequestServices);
        response.StatusCode = ResultResponses.StatusCodeFor(result.Status, options);
        return response.WriteErrorAsync(
            ResultResponses.ToErrorResponse(
                result,
                options,
                ResultResponses.GetJsonOptions(context)),
            cancellationToken);
    }
}
