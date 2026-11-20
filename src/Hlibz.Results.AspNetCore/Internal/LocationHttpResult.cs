namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// Sets the <c>Location</c> header, then sends <paramref name="inner"/>.
/// </summary>
/// <param name="inner">The response.</param>
/// <param name="location">The location.</param>
internal sealed class LocationHttpResult(IResult inner, string location) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        httpContext.Response.Headers.Location = location;
        return inner.ExecuteAsync(httpContext);
    }
}
