using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// Publishes the error code catalog to API clients.
/// </summary>
public static class ErrorCodeEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Why mapping the endpoint isn't trimming- or AOT-safe, as a warning on the caller: the same
    /// as a <c>MapGet</c> given a delegate in a library, which ASP.NET Core's Request Delegate
    /// Generator never sees.
    /// </summary>
    private const string MapGetWarning =
        "MapErrorCodes maps its endpoint with MapGet(pattern, delegate), which binds the handler "
        + "through RequestDelegateFactory at startup.";

    /// <summary>
    /// Maps <c>GET <paramref name="pattern"/></c> to the list of every error code
    /// (<c>{ code, message }</c>) in a registered <see cref="IErrorCodeCatalog"/>, so clients can
    /// see every code a failure's <c>code</c> can hold. Map it once per catalog to expose, say,
    /// the user app's codes and the admin app's codes separately.
    /// </summary>
    /// <param name="endpoints">The route builder.</param>
    /// <param name="pattern">The route, <c>api/error-codes</c> by default.</param>
    /// <param name="catalogName">The name the catalog was registered under with
    /// <see cref="ResultServiceCollectionExtensions.AddErrorCodeCatalog(IServiceCollection, string, IEnumerable{System.Reflection.Assembly})"/>,
    /// or <see langword="null"/> for the default catalog.</param>
    /// <returns>The endpoint's builder, to add a name, tags, a group name or authorization.</returns>
    [RequiresUnreferencedCode(MapGetWarning)]
    [RequiresDynamicCode(MapGetWarning)]
    public static RouteHandlerBuilder MapErrorCodes(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string pattern = "api/error-codes",
        string? catalogName = null)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        // Fail at startup, not on the first request, when the catalog wasn't registered.
        IErrorCodeCatalog catalog = catalogName is null
            ? endpoints.ServiceProvider.GetService<IErrorCodeCatalog>()
              ?? throw new InvalidOperationException(
                  "No default error code catalog is registered; call AddErrorCodeCatalog.")
            : endpoints.ServiceProvider.GetKeyedService<IErrorCodeCatalog>(catalogName)
              ?? throw new InvalidOperationException(
                  $"No error code catalog named '{catalogName}' is registered; call "
                  + $"AddErrorCodeCatalog(\"{catalogName}\", ...).");

        return endpoints.MapGet(pattern, () => TypedResults.Ok(catalog.All));
    }
}
