using System.Text.Json.Serialization;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// The body of every error response (400, 401, 403, 404, 409, 429, ...).
/// </summary>
/// <remarks>
/// Written by the result extensions, the authorization gate and
/// <see cref="HttpResponseErrorExtensions.WriteErrorAsync(HttpResponse, ErrorResponse, CancellationToken)"/>.
/// </remarks>
public sealed class ErrorResponse
{
    /// <summary>
    /// Gets a human-readable description of what went wrong.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the machine-readable error code clients branch on (e.g. <c>orders_1001</c>), or
    /// <c>null</c> when the failure has no specific code.
    /// </summary>
    public required string? Code { get; init; }

    /// <summary>
    /// Gets the validation failures of a 400, keyed by the offending field, each with its
    /// messages; left out of every other error.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }
}
