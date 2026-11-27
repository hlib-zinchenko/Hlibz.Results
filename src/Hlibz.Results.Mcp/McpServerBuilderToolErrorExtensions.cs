using Hlibz.Results.Mcp.Internal;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Hlibz.Results.Mcp;

/// <summary>Adds tool errors with guidance to an MCP server.</summary>
public static class McpServerBuilderToolErrorExtensions
{
    /// <summary>
    /// Turns a tool's <see cref="ToolErrorException"/> (<see cref="ToolResultExtensions.ValueOrThrow{T}"/>)
    /// into a tool error: <c>isError</c>, its code and guidance as text, <c>{ code, details }</c> as
    /// structured content. Lists each tool's errors (<see cref="ToolErrorsAttribute"/>,
    /// <see cref="IToolErrorSource"/>) in its <c>_meta</c> under <see cref="ToolErrorOptions.MetaKey"/>, and
    /// stops the host's start while one has no guidance. Call it as often as needed: each
    /// <paramref name="configure"/> adds to the same options.
    /// </summary>
    /// <param name="builder">The MCP server builder.</param>
    /// <param name="configure">Sets the guidance (<see cref="ToolErrorOptions.Guide{TError}"/>).</param>
    /// <returns>The builder, for chaining.</returns>
    /// <example>
    /// <code>
    /// builder.Services.AddMcpServer()
    ///     .WithToolsFromAssembly()
    ///     .WithToolErrors(errors => errors
    ///         .Guide&lt;CityNotFound&gt;("No city has id {cityId}. Call list_cities to find its id.")
    ///         .Guide&lt;ValidationFailed&gt;("Some arguments are invalid: {errors}. Fix them and call the tool again."));
    /// </code>
    /// </example>
    public static IMcpServerBuilder WithToolErrors(this IMcpServerBuilder builder, Action<ToolErrorOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);
        OptionsBuilder<ToolErrorOptions> options = builder.Services.AddOptions<ToolErrorOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        if (builder.Services.Any(service => service.ServiceType == typeof(ToolErrorCatalog)))
        {
            return builder;
        }

        builder.Services.AddSingleton<ToolErrorCatalog>();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<McpServerOptions>, ToolErrorCatalog>(
            services => services.GetRequiredService<ToolErrorCatalog>()));
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<McpServerOptions>, ToolErrorCatalog>(
            services => services.GetRequiredService<ToolErrorCatalog>()));
        builder.Services.AddOptions<McpServerOptions>().ValidateOnStart();
        return builder.WithRequestFilters(filters => filters.AddCallToolFilter(ToolErrorFilter.Create));
    }
}
