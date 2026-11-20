namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// A minimal API result that builds its real response only when executed, once the request's
/// <see cref="ResultHttpOptions"/>, JSON settings and endpoint can be read.
/// </summary>
/// <param name="build">Builds the response from the request's <see cref="ResultResponseContext"/>.</param>
internal sealed class DeferredHttpResult(Func<ResultResponseContext, IResult> build) : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        IServiceProvider services = httpContext.RequestServices;
        return build(
                ResultResponses.CreateContext(
                    httpContext,
                    ResultResponses.GetOptions(services),
                    ResultResponses.GetHttpJsonOptions(services)))
            .ExecuteAsync(httpContext);
    }
}
