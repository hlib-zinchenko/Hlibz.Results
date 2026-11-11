using Microsoft.AspNetCore.Mvc;

namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// A controller action result that builds its real response only when executed, once the
/// request's <see cref="ResultHttpOptions"/> and MVC JSON settings can be read from its services.
/// </summary>
internal sealed class DeferredActionResult(
    Func<ResultHttpOptions, JsonSerializerOptions, IActionResult> build)
    : ActionResult
{
    public override Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IServiceProvider services = context.HttpContext.RequestServices;
        return build(
                ResultResponses.GetOptions(services),
                ResultResponses.GetMvcJsonOptions(services))
            .ExecuteResultAsync(context);
    }
}
