namespace Hlibz.Results.AspNetCore;

/// <summary>
/// Everything an envelope can carry, handed to <see cref="ResultHttpOptions.CreateEnvelope"/>
/// so an API can wrap its responses in its own shape.
/// </summary>
public sealed class EnvelopeContent
{
    /// <summary>
    /// Gets a value indicating whether the request succeeded.
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the HTTP status code the outcome maps to (<see cref="ResultHttpOptions.StatusCodes"/>),
    /// even when <see cref="ResultHttpOptions.EnvelopeAlwaysOk"/> sends <c>200</c> instead.
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// Gets the result's status, or <see langword="null"/> for an error written outside an
    /// endpoint (the auth gate, <see cref="HttpResponseErrorExtensions"/>).
    /// </summary>
    public ResultStatus? ResultStatus { get; init; }

    /// <summary>
    /// Gets the outcome's message: the failure's own, or the default for its status.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the failure's error code, or <see langword="null"/>.
    /// </summary>
    public string? Code { get; init; }

    /// <summary>
    /// Gets the validation errors, keys already cased, or <see langword="null"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }

    /// <summary>
    /// Gets the value of a successful <see cref="Result{T}"/>; <see langword="null"/> otherwise.
    /// </summary>
    public object? Data { get; init; }
}
