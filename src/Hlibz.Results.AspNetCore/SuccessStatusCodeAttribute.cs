namespace Hlibz.Results.AspNetCore;

/// <summary>
/// The HTTP status code a successful result is sent with from this endpoint, in place of
/// <see cref="ResultHttpOptions.SuccessStatusCodes"/> and
/// <see cref="ResultHttpOptions.StatusCodes"/>. Put it on a minimal API handler, a controller
/// action or a controller class, or add it to an endpoint or a route group with
/// <see cref="ResultEndpointConventionBuilderExtensions.WithSuccessStatusCode{TBuilder}"/>; the
/// most specific one wins (an endpoint's over its group's, an action's over its controller's).
/// </summary>
/// <remarks>
/// A <c>204</c> is sent without a body, so a <see cref="Result{T}"/>'s value is dropped, and it
/// can't be used with envelopes, which always have one.
/// </remarks>
/// <example>
/// <code>
/// [HttpPost, SuccessStatusCode(StatusCodes.Status202Accepted)]
/// public ActionResult&lt;ImportDto&gt; Import(ImportRequest request) =&gt; imports.Start(request).ToActionResult();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public sealed class SuccessStatusCodeAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SuccessStatusCodeAttribute"/> class.
    /// </summary>
    /// <param name="statusCode">A <c>2xx</c> status code.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="statusCode"/> isn't
    /// <c>2xx</c>.</exception>
    public SuccessStatusCodeAttribute(int statusCode)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(statusCode, 200);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(statusCode, 299);
        StatusCode = statusCode;
    }

    /// <summary>
    /// Gets the status code a successful result is sent with.
    /// </summary>
    public int StatusCode { get; }
}
