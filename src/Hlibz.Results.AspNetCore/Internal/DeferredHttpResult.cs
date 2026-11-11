namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// A minimal API result that builds its real response only when executed, once the request's
/// <see cref="ResultHttpOptions"/> and JSON settings can be read from its services.
/// </summary>
internal sealed class DeferredHttpResult(
    Func<ResultHttpOptions, JsonSerializerOptions, IResult> build)
    : IResult
{
    public Task ExecuteAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        IServiceProvider services = httpContext.RequestServices;
        return build(
                ResultResponses.GetOptions(services),
                ResultResponses.GetHttpJsonOptions(services))
            .ExecuteAsync(httpContext);
    }
}
