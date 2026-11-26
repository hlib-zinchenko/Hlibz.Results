using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;

using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;
using MvcJsonOptions = Microsoft.AspNetCore.Mvc.JsonOptions;

namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// Builds the status code and body of a result's response, shared by minimal APIs, controllers
/// and the error writers so every path produces the same shape.
/// </summary>
internal static class ResultResponses
{
    private static readonly JsonSerializerOptions WebDefaults = new(JsonSerializerDefaults.Web);

    public static int StatusCodeFor(ResultStatus status, ResultHttpOptions options) =>
        options.StatusCodes.TryGetValue(status, out int statusCode)
            ? statusCode
            : throw new InvalidOperationException(
                $"{nameof(ResultHttpOptions)}.{nameof(ResultHttpOptions.StatusCodes)} has no "
                + $"HTTP status code for {nameof(ResultStatus)}.{status}.");

    /// <summary>
    /// The status code a successful result is sent with when nothing more specific (a
    /// <c>ToCreated…</c> method, <see cref="CreatedLocationMetadata"/>) decides: the endpoint's
    /// <see cref="SuccessStatusCodeAttribute"/>, then the default for the request's method, then
    /// the default for <see cref="ResultStatus.Success"/>.
    /// </summary>
    /// <param name="attribute">The endpoint's last <see cref="SuccessStatusCodeAttribute"/>, if
    /// any.</param>
    /// <param name="httpMethod">The request's method, or <see langword="null"/> when it isn't
    /// known (documenting an endpoint that answers several).</param>
    /// <param name="options">The options.</param>
    /// <returns>A <c>2xx</c> status code.</returns>
    public static int SuccessStatusCodeFor(
        SuccessStatusCodeAttribute? attribute,
        string? httpMethod,
        ResultHttpOptions options)
    {
        if (attribute is not null)
        {
            return attribute.StatusCode;
        }

        if (httpMethod is not null
            && options.SuccessStatusCodes.TryGetValue(httpMethod, out int byMethod))
        {
            return RequireSuccess(
                byMethod,
                $"{nameof(ResultHttpOptions)}.{nameof(ResultHttpOptions.SuccessStatusCodes)}[\"{httpMethod}\"]");
        }

        return RequireSuccess(
            StatusCodeFor(ResultStatus.Success, options),
            $"{nameof(ResultHttpOptions)}.{nameof(ResultHttpOptions.StatusCodes)}[{nameof(ResultStatus)}.{nameof(ResultStatus.Success)}]");
    }

    /// <summary>
    /// Everything a result's response depends on for the request being handled: the options, the
    /// JSON settings, and the endpoint's success status code and location.
    /// </summary>
    /// <param name="httpContext">The request.</param>
    /// <param name="options">The options.</param>
    /// <param name="json">The JSON settings.</param>
    /// <returns>The context.</returns>
    public static ResultResponseContext CreateContext(
        HttpContext httpContext,
        ResultHttpOptions options,
        JsonSerializerOptions json)
    {
        EndpointMetadataCollection? metadata = httpContext.GetEndpoint()?.Metadata;
        CreatedLocationMetadata? created = metadata?.GetMetadata<CreatedLocationMetadata>();
        return new ResultResponseContext
        {
            Options = options,
            Json = json,
            CreatedLocation = created,
            SuccessStatusCode = created is null
                ? SuccessStatusCodeFor(
                    metadata?.GetMetadata<SuccessStatusCodeAttribute>(),
                    httpContext.Request.Method,
                    options)
                : StatusCodes.Status201Created,
        };
    }

    /// <summary>
    /// The status code an envelope carries: <paramref name="successStatusCode"/> on success, the
    /// failure's own otherwise.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="successStatusCode">The success status code.</param>
    /// <param name="options">The options.</param>
    /// <returns>The status code.</returns>
    /// <exception cref="InvalidOperationException">A success would be a <c>204</c>, which can't
    /// carry the envelope.</exception>
    public static int EnvelopeStatusCodeFor(
        Result result,
        int successStatusCode,
        ResultHttpOptions options)
    {
        if (!result.IsSuccess)
        {
            return StatusCodeFor(result.Status, options);
        }

        RequireEnvelopeBody(successStatusCode);
        return successStatusCode;
    }

    /// <summary>
    /// Throws for a <c>204</c>: an envelope always has a body, and a <c>204</c> can't.
    /// </summary>
    /// <param name="successStatusCode">The success status code.</param>
    /// <exception cref="InvalidOperationException"><paramref name="successStatusCode"/> is
    /// <c>204</c>.</exception>
    public static void RequireEnvelopeBody(int successStatusCode)
    {
        if (successStatusCode == StatusCodes.Status204NoContent)
        {
            throw new InvalidOperationException(
                "An envelope can't be sent as 204 No Content, which has no body: give the endpoint "
                + "another success status code.");
        }
    }

    public static ErrorResponse ToErrorResponse(
        Error error,
        ResultHttpOptions options,
        JsonSerializerOptions serializerOptions) =>
        new()
        {
            Message = error.Message,
            Code = error.Code,
            ValidationErrors = ValidationErrorsOf(error, options, serializerOptions),
            Details = DetailsOf(error, serializerOptions),
        };

    /// <summary>
    /// What goes out as an error's details: the error itself, when it carries data of its own
    /// (a <see cref="ValidationFailed"/>'s goes out as the validation errors instead) and
    /// <paramref name="json"/> can write its type. An error response must not fail, so an error
    /// type missing from a source-generated-only setup (Native AOT) goes out without details.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="json">The JSON options the response is written with.</param>
    /// <returns>The details, or <see langword="null"/>.</returns>
    public static Error? DetailsOf(Error error, JsonSerializerOptions json) =>
        error is not ValidationFailed
        && error.Descriptor.HasDetails
        && json.TryGetTypeInfo(error.GetType(), out _)
            ? error
            : null;

    /// <summary>
    /// A <see cref="ValidationFailed"/>'s messages, keyed as <paramref name="options"/> say.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <param name="options">The options.</param>
    /// <param name="serializerOptions">The JSON options, for their naming policy.</param>
    /// <returns>The messages, or <see langword="null"/> for any other error.</returns>
    public static IReadOnlyDictionary<string, string[]>? ValidationErrorsOf(
        Error error,
        ResultHttpOptions options,
        JsonSerializerOptions serializerOptions) =>
        error is ValidationFailed validation
            ? FormatKeys(validation.Errors, options, serializerOptions)
            : null;

    public static object ToEnvelope(
        Result result,
        int statusCode,
        ResultHttpOptions options,
        JsonSerializerOptions serializerOptions)
    {
        EnvelopeContent content = ToContent(
            result,
            data: null,
            statusCode,
            options,
            serializerOptions);
        return Create(content, options) ?? Builtin(content);
    }

    public static object ToEnvelope<T>(
        Result<T> result,
        int statusCode,
        ResultHttpOptions options,
        JsonSerializerOptions serializerOptions)
    {
        if (typeof(T) == typeof(Unit))
        {
            return ToEnvelope((Result)result, statusCode, options, serializerOptions);
        }

        EnvelopeContent content = ToContent(
            result,
            result.Value,
            statusCode,
            options,
            serializerOptions);
        return Create(content, options)
               ?? new ResultEnvelope<T>
               {
                   IsSuccess = content.IsSuccess,
                   Status = content.StatusCode,
                   Message = content.Message,
                   Code = content.Code,
                   ValidationErrors = content.ValidationErrors,
                   Details = content.Details,
                   Data = result.Value,
               };
    }

    public static object ToEnvelope(ErrorResponse error, int statusCode, ResultHttpOptions options)
    {
        EnvelopeContent content = new()
        {
            IsSuccess = false,
            StatusCode = statusCode,
            Message = error.Message,
            Code = error.Code,
            ValidationErrors = error.ValidationErrors,
            Details = error.Details,
        };
        return Create(content, options) ?? Builtin(content);
    }

    /// <summary>
    /// The HTTP status an envelope goes out with: its own, or 200 under
    /// <see cref="ResultHttpOptions.EnvelopeAlwaysOk"/>.
    /// </summary>
    /// <param name="statusCode">The outcome's status code.</param>
    /// <param name="options">The options.</param>
    /// <returns>The status code to send.</returns>
    public static int EnvelopeHttpStatusCode(int statusCode, ResultHttpOptions options) =>
        options.EnvelopeAlwaysOk ? StatusCodes.Status200OK : statusCode;

    public static IReadOnlyDictionary<string, string[]>? FormatKeys(
        IReadOnlyDictionary<string, string[]>? validationErrors,
        ResultHttpOptions options,
        JsonSerializerOptions serializerOptions)
    {
        if (validationErrors is null)
        {
            return null;
        }

        Func<string, string>? format = options.FormatValidationKey;
        if (format is null
            && options.ValidationKeyNaming == ValidationKeyNaming.JsonNamingPolicy
            && serializerOptions.PropertyNamingPolicy is { } policy)
        {
            format = key => ValidationKeyFormatter.Format(key, policy);
        }

        if (format is null)
        {
            return validationErrors;
        }

        // Two paths can collapse to one key (e.g. "Name" and "name"): their messages merge.
        Dictionary<string, string[]> formatted = new(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string[]> pair in validationErrors)
        {
            string key = format(pair.Key);
            formatted[key] = formatted.TryGetValue(key, out string[]? existing)
                ? [.. existing, .. pair.Value]
                : pair.Value;
        }

        return formatted;
    }

    /// <summary>
    /// The metadata <paramref name="json"/> serializes a <typeparamref name="T"/> with: through
    /// its resolvers, so the app's own settings apply and, under Native AOT, a source-generated
    /// context (<see cref="ResultsJsonContext"/> or the app's) rather than reflection.
    /// </summary>
    /// <typeparam name="T">The type to write.</typeparam>
    /// <param name="json">The JSON settings.</param>
    /// <returns>The metadata.</returns>
    [SuppressMessage(
        "Usage",
        "CA2263:Prefer generic overload when type is known",
        Justification = "GetTypeInfo<T>() is new in .NET 11; this compiles for .NET 10 too.")]
    public static System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> TypeInfo<T>(JsonSerializerOptions json) =>
        (System.Text.Json.Serialization.Metadata.JsonTypeInfo<T>)json.GetTypeInfo(typeof(T));

    public static ResultHttpOptions GetOptions(IServiceProvider services) =>
        services.GetService<IOptions<ResultHttpOptions>>()?.Value ?? new ResultHttpOptions();

    public static JsonSerializerOptions GetHttpJsonOptions(IServiceProvider services) =>
        services.GetService<IOptions<HttpJsonOptions>>()?.Value.SerializerOptions
        ?? WebDefaults;

    public static JsonSerializerOptions GetMvcJsonOptions(IServiceProvider services) =>
        services.GetService<IOptions<MvcJsonOptions>>()?.Value.JsonSerializerOptions
        ?? WebDefaults;

    /// <summary>
    /// Picks the JSON settings of whatever handles the request: MVC's for a controller action,
    /// the minimal API ones otherwise.
    /// </summary>
    /// <param name="context">The request.</param>
    /// <returns>The JSON settings.</returns>
    public static JsonSerializerOptions GetJsonOptions(HttpContext context) =>
        context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is null
            ? GetHttpJsonOptions(context.RequestServices)
            : GetMvcJsonOptions(context.RequestServices);

    private static EnvelopeContent ToContent(
        Result result,
        object? data,
        int statusCode,
        ResultHttpOptions options,
        JsonSerializerOptions serializerOptions) =>
        new()
        {
            IsSuccess = result.IsSuccess,
            StatusCode = statusCode,
            ResultStatus = result.Status,
            Message = result.Error?.Message ?? options.SuccessMessage,
            Code = result.Error?.Code,
            ValidationErrors = result.Error is { } error
                ? ValidationErrorsOf(error, options, serializerOptions)
                : null,
            Details = result.Error is { } failure ? DetailsOf(failure, serializerOptions) : null,
            Error = result.Error,
            Data = data,
        };

    private static int RequireSuccess(int statusCode, string source) =>
        statusCode is >= 200 and <= 299
            ? statusCode
            : throw new InvalidOperationException(
                $"{source} is {statusCode}, but a successful result needs a 2xx status code.");

    private static object? Create(EnvelopeContent content, ResultHttpOptions options) =>
        options.CreateEnvelope is { } create
            ? create(content)
              ?? throw new InvalidOperationException(
                  $"{nameof(ResultHttpOptions)}.{nameof(ResultHttpOptions.CreateEnvelope)} "
                  + "returned null.")
            : null;

    private static ResultEnvelope Builtin(EnvelopeContent content) =>
        new()
        {
            IsSuccess = content.IsSuccess,
            Status = content.StatusCode,
            Message = content.Message,
            Code = content.Code,
            ValidationErrors = content.ValidationErrors,
            Details = content.Details,
        };
}
