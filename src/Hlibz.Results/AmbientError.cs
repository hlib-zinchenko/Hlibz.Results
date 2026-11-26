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
/// Something the operation depends on is temporarily unavailable; the caller may retry later.
/// </summary>
[Error("unavailable", ResultStatus.Unavailable, "The service is temporarily unavailable. Please retry later.")]
public sealed record Unavailable : AmbientError;
