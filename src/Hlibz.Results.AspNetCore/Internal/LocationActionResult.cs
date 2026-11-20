using Microsoft.AspNetCore.Mvc;

namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// Sets the <c>Location</c> header, then sends <paramref name="inner"/>.
/// </summary>
/// <param name="inner">The response.</param>
/// <param name="location">The location.</param>
internal sealed class LocationActionResult(IActionResult inner, string location) : ActionResult
{
    public override Task ExecuteResultAsync(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.HttpContext.Response.Headers.Location = location;
        return inner.ExecuteResultAsync(context);
    }
}
