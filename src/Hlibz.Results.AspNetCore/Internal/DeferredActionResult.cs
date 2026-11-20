using Microsoft.AspNetCore.Mvc;

namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// A controller action result that builds its real response only when executed, once the
/// request's <see cref="ResultHttpOptions"/>, MVC JSON settings and endpoint can be read.
/// </summary>
internal sealed class DeferredActionResult(Func<ResultResponseContext, IActionResult> build)
    : ActionResult
{
    public override Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        HttpContext httpContext = context.HttpContext;
        IServiceProvider services = httpContext.RequestServices;
        return build(
                ResultResponses.CreateContext(
                    httpContext,
                    ResultResponses.GetOptions(services),
                    ResultResponses.GetMvcJsonOptions(services)))
            .ExecuteResultAsync(context);
    }
}
