using System.Reflection;

using Hlibz.Results.AspNetCore.Internal;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// Registers the result-to-HTTP conventions.
/// </summary>
public static class ResultServiceCollectionExtensions
{
    /// <summary>
    /// Configures how results are written (<see cref="ResultHttpOptions"/>) and makes the
    /// authorization gate's <c>401</c>/<c>403</c> carry the same error body as every other
    /// failure. Replaces any other <see cref="IAuthorizationMiddlewareResultHandler"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Changes to the default options, if any.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    public static IServiceCollection AddResultErrorResponses(
        this IServiceCollection services,
        Action<ResultHttpOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        OptionsBuilder<ResultHttpOptions> builder = services.AddOptions<ResultHttpOptions>();
        if (configure is not null)
        {
            builder.Configure(configure);
        }

        services.AddSingleton<
            IAuthorizationMiddlewareResultHandler,
            ErrorResponseAuthorizationResultHandler>();
        return services;
    }

    /// <summary>
    /// Registers the default <see cref="IErrorCodeCatalog"/>: every <see cref="ErrorCode"/>
    /// declared in <paramref name="assemblies"/>, scanned now so duplicate codes fail at startup.
    /// Resolve it as a plain <see cref="IErrorCodeCatalog"/>, or publish it with
    /// <see cref="ErrorCodeEndpointRouteBuilderExtensions.MapErrorCodes"/>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="assemblies">The assemblies declaring the error codes.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    /// <exception cref="InvalidOperationException">Two error codes share a code.</exception>
    public static IServiceCollection AddErrorCodeCatalog(
        this IServiceCollection services,
        params IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services.AddSingleton<IErrorCodeCatalog>(CreateValidated(assemblies));
    }

    /// <summary>
    /// Registers a named <see cref="IErrorCodeCatalog"/> of the error codes declared in
    /// <paramref name="assemblies"/>, so each app or audience can publish only its own codes, e.g.
    /// a public catalog of the user-facing modules and an <c>"admin"</c> one that adds the admin
    /// modules. The same assembly may be in several catalogs. Resolve it as a keyed service
    /// (<c>[FromKeyedServices("admin")] IErrorCodeCatalog</c>) or publish it with
    /// <c>MapErrorCodes(pattern, "admin")</c>.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="name">The catalog's name, its service key.</param>
    /// <param name="assemblies">The assemblies declaring the error codes.</param>
    /// <returns>The same <paramref name="services"/>, for chaining.</returns>
    /// <exception cref="InvalidOperationException">Two error codes in the catalog share a code.</exception>
    public static IServiceCollection AddErrorCodeCatalog(
        this IServiceCollection services,
        string name,
        params IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return services.AddKeyedSingleton<IErrorCodeCatalog>(name, CreateValidated(assemblies));
    }

    private static ErrorCodeCatalog CreateValidated(IEnumerable<Assembly> assemblies)
    {
        ErrorCodeCatalog catalog = new(assemblies);
        catalog.Validate();
        return catalog;
    }
}
