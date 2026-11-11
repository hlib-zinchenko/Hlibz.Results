namespace Hlibz.Results.AspNetCore;

/// <summary>
/// How results and errors are written as HTTP responses. Configure with
/// <see cref="ResultServiceCollectionExtensions.AddResultErrorResponses"/>; the defaults apply
/// without it.
/// </summary>
public sealed class ResultHttpOptions
{
    /// <summary>
    /// Gets the HTTP status code sent for each <see cref="ResultStatus"/>. Defaults:
    /// <c>Success</c> 200, <c>Error</c> 400, <c>Invalid</c> 400, <c>NotFound</c> 404,
    /// <c>Unauthorized</c> 401, <c>Forbidden</c> 403, <c>Conflict</c> 409, <c>Unavailable</c> 503.
    /// Change an entry to follow your API's conventions, e.g.
    /// <c>options.StatusCodes[ResultStatus.Invalid] = StatusCodes.Status422UnprocessableEntity</c>.
    /// </summary>
    public IDictionary<ResultStatus, int> StatusCodes { get; } = new Dictionary<ResultStatus, int>
    {
        [ResultStatus.Success] = Microsoft.AspNetCore.Http.StatusCodes.Status200OK,
        [ResultStatus.Error] = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest,
        [ResultStatus.Invalid] = Microsoft.AspNetCore.Http.StatusCodes.Status400BadRequest,
        [ResultStatus.NotFound] = Microsoft.AspNetCore.Http.StatusCodes.Status404NotFound,
        [ResultStatus.Unauthorized] = Microsoft.AspNetCore.Http.StatusCodes.Status401Unauthorized,
        [ResultStatus.Forbidden] = Microsoft.AspNetCore.Http.StatusCodes.Status403Forbidden,
        [ResultStatus.Conflict] = Microsoft.AspNetCore.Http.StatusCodes.Status409Conflict,
        [ResultStatus.Unavailable] =
            Microsoft.AspNetCore.Http.StatusCodes.Status503ServiceUnavailable,
    };

    /// <summary>
    /// Gets or sets how validation error keys are written. Defaults to
    /// <see cref="ValidationKeyNaming.JsonNamingPolicy"/>.
    /// </summary>
    public ValidationKeyNaming ValidationKeyNaming { get; set; } =
        ValidationKeyNaming.JsonNamingPolicy;

    /// <summary>
    /// Gets or sets a custom conversion of each validation error key, used instead of
    /// <see cref="ValidationKeyNaming"/> when set.
    /// </summary>
    public Func<string, string>? FormatValidationKey { get; set; }

    /// <summary>
    /// Gets the messages used when a failure carries none of its own.
    /// </summary>
    public ResultMessages Messages { get; } = new();

    /// <summary>
    /// Gets or sets a factory for an API's own envelope shape, used by the <c>ToEnvelope…</c>
    /// methods and, with <see cref="UseEnvelope"/>, by the error writers; <see langword="null"/>
    /// for <see cref="ResultEnvelope"/>. The returned object is serialized by its runtime type
    /// with the app's JSON settings. Describe the shape in OpenAPI yourself (e.g.
    /// <c>.Produces&lt;MyEnvelope&lt;OrderDto&gt;&gt;()</c>), since the methods' declared types
    /// name the built-in envelope.
    /// </summary>
    /// <example>
    /// <code>
    /// options.CreateEnvelope = content => new
    /// {
    ///     ok = content.IsSuccess,
    ///     error = content.IsSuccess ? null : new { content.Code, content.Message },
    ///     payload = content.Data,
    /// };
    /// </code>
    /// </example>
    public Func<EnvelopeContent, object>? CreateEnvelope { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether envelope responses are always sent as HTTP
    /// <c>200</c>, failures included, with the real status only in the envelope, for clients
    /// that read the outcome from the body alone. Applies to the <c>ToEnvelope…</c> methods and,
    /// with <see cref="UseEnvelope"/>, to the error writers and the auth gate; plain
    /// <see cref="ErrorResponse"/> bodies keep their status code, since they don't carry it.
    /// </summary>
    public bool EnvelopeAlwaysOk { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether errors written outside an endpoint (the auth gate,
    /// <see cref="HttpResponseErrorExtensions"/>) use <see cref="ResultEnvelope"/> rather than
    /// <see cref="ErrorResponse"/>. Turn it on when the endpoints use the <c>ToEnvelope…</c>
    /// methods, so every body has the same shape.
    /// </summary>
    public bool UseEnvelope { get; set; }
}
