using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Hlibz.Results.Mcp.Internal;

/// <summary>Turns a tool's <see cref="ToolErrorException"/> into a failed tool result with guidance.</summary>
internal static class ToolErrorFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Create(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next) =>
        async (context, cancellationToken) =>
        {
            try
            {
                return await next(context, cancellationToken).ConfigureAwait(false);
            }
            catch (ToolErrorException failure)
            {
                ToolErrorOptions options = context.Services?.GetService<IOptions<ToolErrorOptions>>()?.Value ?? new();
                return ToolErrorResults.Create(failure.Error, options);
            }
        };
}
