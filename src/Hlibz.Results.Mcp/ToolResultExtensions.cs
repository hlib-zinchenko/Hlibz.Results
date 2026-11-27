namespace Hlibz.Results.Mcp;

/// <summary>
/// Unwraps a result in a tool: the value, or a <see cref="ToolErrorException"/> that the filter
/// <see cref="McpServerBuilderToolErrorExtensions.WithToolErrors"/> adds turns into a tool error
/// with guidance. MCP tools return their value, which the SDK serializes before any filter sees it,
/// so a tool can't return the result itself.
/// </summary>
public static class ToolResultExtensions
{
    /// <summary>Gets the value of a successful result.</summary>
    /// <typeparam name="T">The value's type.</typeparam>
    /// <param name="result">The result; a <see cref="Result{T, TError}"/> too.</param>
    /// <returns>The value.</returns>
    /// <exception cref="ToolErrorException">The result failed.</exception>
    public static T ValueOrThrow<T>(this Result<T> result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Error is { } error ? throw new ToolErrorException(error) : result.Value!;
    }

    /// <summary>Throws when the result failed.</summary>
    /// <param name="result">The result.</param>
    /// <exception cref="ToolErrorException">The result failed.</exception>
    public static void ThrowIfFailed(this Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Error is { } error)
        {
            throw new ToolErrorException(error);
        }
    }
}
