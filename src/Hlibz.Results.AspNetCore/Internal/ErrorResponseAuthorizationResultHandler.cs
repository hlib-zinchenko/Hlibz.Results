using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.Extensions.Options;

namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// Gives the authorization gate's own <c>401</c>/<c>403</c> (no, invalid or expired credentials;
/// a failed policy) the same body as every other error, on top of what the default handler does
/// (status code, <c>WWW-Authenticate</c>).
/// </summary>
internal sealed class ErrorResponseAuthorizationResultHandler(IOptions<ResultHttpOptions> options)
    : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _defaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        await _defaultHandler
            .HandleAsync(next, context, policy, authorizeResult)
            .ConfigureAwait(false);

        // A scheme that answers with a redirect (cookies: 302 to the login or access denied page)
        // keeps its response: a body there means nothing, and with EnvelopeAlwaysOk it would turn
        // the redirect into a 200 the browser doesn't follow.
        if (authorizeResult.Succeeded
            || context.Response.HasStarted
            || context.Response.StatusCode is not (StatusCodes.Status401Unauthorized
                or StatusCodes.Status403Forbidden))
        {
            return;
        }

        ResultMessages messages = options.Value.Messages;
        string message = authorizeResult.Challenged ? messages.Unauthorized : messages.Forbidden;

        await context.Response
            .WriteErrorAsync(message, code: null, context.RequestAborted)
            .ConfigureAwait(false);
    }
}
