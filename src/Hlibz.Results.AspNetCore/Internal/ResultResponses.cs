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

    public static string DefaultMessageFor(int statusCode, ResultMessages messages) =>
        statusCode switch
        {
            StatusCodes.Status401Unauthorized => messages.Unauthorized,
            StatusCodes.Status403Forbidden => messages.Forbidden,
            StatusCodes.Status404NotFound => messages.NotFound,
            StatusCodes.Status409Conflict => messages.Conflict,
            StatusCodes.Status422UnprocessableEntity => messages.Invalid,
            StatusCodes.Status503ServiceUnavailable => messages.Unavailable,
            < 400 => messages.Success,
            _ => messages.BadRequest,
        };

    public static ErrorResponse ToErrorResponse(
        Result result,
        ResultHttpOptions options,
        JsonSerializerOptions serializerOptions) =>
        new()
        {
            Message = result.ErrorMessage ?? options.Messages.For(result.Status),
            Code = result.ErrorCode,
            ValidationErrors = FormatKeys(result.ValidationErrors, options, serializerOptions),
        };

    public static object ToEnvelope(
        Result result,
        ResultHttpOptions options,
        JsonSerializerOptions serializerOptions)
    {
        EnvelopeContent content = ToContent(result, data: null, options, serializerOptions);
        return Create(content, options) ?? Builtin(content);
    }

    public static object ToEnvelope<T>(
        Result<T> result,
        ResultHttpOptions options,
        JsonSerializerOptions serializerOptions)
    {
        EnvelopeContent content = ToContent(result, result.Value, options, serializerOptions);
        return Create(content, options)
               ?? new ResultEnvelope<T>
               {
                   IsSuccess = content.IsSuccess,
                   Status = content.StatusCode,
                   Message = content.Message,
                   Code = content.Code,
                   ValidationErrors = content.ValidationErrors,
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
        ResultHttpOptions options,
        JsonSerializerOptions serializerOptions) =>
        new()
        {
            IsSuccess = result.IsSuccess,
            StatusCode = StatusCodeFor(result.Status, options),
            ResultStatus = result.Status,
            Message = result.ErrorMessage ?? options.Messages.For(result.Status),
            Code = result.ErrorCode,
            ValidationErrors = FormatKeys(result.ValidationErrors, options, serializerOptions),
            Data = data,
        };

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
        };
}
