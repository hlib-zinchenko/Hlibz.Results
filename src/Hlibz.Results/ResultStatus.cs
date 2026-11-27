namespace Hlibz.Results;

/// <summary>
/// The outcome of an operation. Every status but <see cref="Success"/> is a failure, and is what an <see cref="Error"/>'s
/// <see cref="ErrorAttribute"/> says failed.
/// </summary>
public enum ResultStatus
{
    /// <summary>
    /// The operation succeeded.
    /// </summary>
    Success = 0,

    /// <summary>
    /// The request was rejected: a broken business rule or another bad request (HTTP 400 by
    /// default).
    /// </summary>
    Error,

    /// <summary>
    /// The request's input failed validation (<see cref="ValidationFailed"/>, with the messages) (HTTP 400 by default, 422 for APIs that tell
    /// validation apart).
    /// </summary>
    Invalid,

    /// <summary>
    /// What the request refers to doesn't exist, or isn't visible to the caller (HTTP 404).
    /// </summary>
    NotFound,

    /// <summary>
    /// The caller isn't authenticated, or their credentials were rejected (HTTP 401).
    /// </summary>
    Unauthorized,

    /// <summary>
    /// The caller is authenticated but not allowed to do this (HTTP 403).
    /// </summary>
    Forbidden,

    /// <summary>
    /// The request conflicts with the current state, e.g. a concurrent change or a duplicate
    /// (HTTP 409).
    /// </summary>
    Conflict,

    /// <summary>
    /// Something the operation depends on is temporarily unavailable, e.g. an external service is
    /// down; the caller may retry later (HTTP 503).
    /// </summary>
    Unavailable,

    /// <summary>
    /// The caller sent too many requests, e.g. a rate limit turned this one away; it may retry
    /// later (HTTP 429).
    /// </summary>
    TooManyRequests,
}
