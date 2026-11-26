using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// One entry of a published error code catalog
/// (<see cref="ErrorCodeEndpointRouteBuilderExtensions.MapErrorCodes"/>, the build-time files):
/// what a client needs to handle the error.
/// </summary>
public sealed class ErrorCodeInfo
{
    /// <summary>
    /// Gets the code clients branch on.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the HTTP status code the error is sent with (<see cref="ResultHttpOptions.StatusCodes"/>).
    /// </summary>
    public required int Status { get; init; }

    /// <summary>
    /// Gets the error's message.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the JSON schema of the error's details (<see cref="ErrorResponse.Details"/>); left out
    /// of an error without data of its own, and when the app's JSON options can't describe its
    /// type (under Native AOT, one missing from the app's <c>JsonSerializerContext</c>).
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonNode? Details { get; init; }
}
