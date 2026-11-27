namespace Hlibz.Results;

/// <summary>
/// An error any operation can fail with, added by the layers around it (validation, a filter,
/// authorization, a dependency) rather than declared by the operation: a
/// <see cref="Result{T, TError}"/> accepts one whatever its error set. The set is closed: these
/// are all of them.
/// </summary>
public closed record AmbientError : Error;

/// <summary>
/// The input failed validation.
/// </summary>
/// <param name="Errors">The messages, keyed by the offending property path as given (e.g.
/// <c>Items[0].Quantity</c>); casing them is the HTTP layer's job. Never empty.</param>
[Error("validation_failed", ResultStatus.Invalid, "Validation failed. Please check the validation errors.")]
public sealed record ValidationFailed(IReadOnlyDictionary<string, string[]> Errors) : AmbientError
{
    /// <summary>
    /// Gets the messages, keyed by the offending property path.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Errors { get; } = RequireErrors(Errors);

    private static IReadOnlyDictionary<string, string[]> RequireErrors(
        IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors.Count > 0
            ? errors
            : throw new ArgumentException("A validation failure needs at least one error.", nameof(errors));
    }
}

/// <summary>
/// The caller isn't authenticated, or their credentials were rejected.
/// </summary>
[Error("unauthorized", ResultStatus.Unauthorized, "Authentication is required.")]
public sealed record Unauthorized : AmbientError;

/// <summary>
/// The caller is authenticated but not allowed to do this.
/// </summary>
[Error("forbidden", ResultStatus.Forbidden, "Access to the resource is forbidden.")]
public sealed record Forbidden : AmbientError;

/// <summary>
/// Someone else changed the data since the caller read it (an optimistic concurrency check
/// failed, say a row version that no longer matches); the caller may reload and retry.
/// </summary>
[Error("concurrency_conflict", ResultStatus.Conflict, "The data was changed by someone else. Please reload and retry.")]
public sealed record ConcurrencyConflict : AmbientError;

/// <summary>
/// The caller sent too many requests: a rate limit turned this one away. It may retry later,
/// when the <c>Retry-After</c> header says if one is sent.
/// </summary>
[Error("rate_limited", ResultStatus.TooManyRequests, "Too many requests. Please retry later.")]
public sealed record RateLimited : AmbientError;

/// <summary>
/// Something the operation depends on is temporarily unavailable; the caller may retry later.
/// </summary>
[Error("unavailable", ResultStatus.Unavailable, "The service is temporarily unavailable. Please retry later.")]
public sealed record Unavailable : AmbientError;
