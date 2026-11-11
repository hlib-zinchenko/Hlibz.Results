using System.Text.Json.Serialization;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// The opt-in envelope every response is wrapped in, success included, for APIs whose clients
/// expect one shape for every body. Made by <c>ToEnvelopeHttpResult()</c> /
/// <c>ToEnvelopeActionResult()</c>, and by the error writers when
/// <see cref="ResultHttpOptions.UseEnvelope"/> is on.
/// </summary>
public class ResultEnvelope
{
    /// <summary>
    /// Gets a value indicating whether the request succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the HTTP status code of the response.
    /// </summary>
    public required int Status { get; init; }

    /// <summary>
    /// Gets a human-readable description of the outcome, for client developers rather than end
    /// users.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the machine-readable error code of a failure, or <c>null</c>.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Gets the validation failures of a 400, keyed by the offending field; left out otherwise.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }
}

/// <summary>
/// The opt-in envelope of a response carrying data on success.
/// </summary>
/// <typeparam name="T">The type of the data.</typeparam>
#pragma warning disable SA1402
public sealed class ResultEnvelope<T> : ResultEnvelope
#pragma warning restore SA1402
{
    /// <summary>
    /// Gets the data on success; <c>null</c> on failure.
    /// </summary>
    public T? Data { get; init; }
}
