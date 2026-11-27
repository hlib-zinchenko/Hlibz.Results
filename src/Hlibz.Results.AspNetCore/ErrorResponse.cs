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
    /// Gets the machine-readable error code clients branch on (e.g. <c>orders_1001</c>): every
    /// error has one, the ones the API's catalog lists.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the validation failures of a 400, keyed by the offending field, each with its
    /// messages; left out of every other error.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    /// <summary>
    /// Gets the error's own data (the error record, e.g. the ids of what blocks a delete), so a
    /// client can act on the failure without another request; left out of an error without any.
    /// </summary>
    /// <remarks>
    /// Written as its runtime type. Under Native AOT, add each error type with data to the app's
    /// <c>JsonSerializerContext</c>: one the JSON options can't write goes out without details
    /// rather than failing the response.
    /// </remarks>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public object? Details { get; init; }
}
