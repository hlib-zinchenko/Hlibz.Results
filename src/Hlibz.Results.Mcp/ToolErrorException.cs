namespace Hlibz.Results.Mcp;

/// <summary>
/// A tool failed with <see cref="Error"/>: what <see cref="ToolResultExtensions.ValueOrThrow{T}"/>
/// throws, and what the filter <see cref="McpServerBuilderToolErrorExtensions.WithToolErrors"/> adds
/// turns into a tool error with guidance. Without that filter it is still an
/// <see cref="McpException"/>, which the MCP SDK sends as a tool error whose text is the code and
/// message.
/// </summary>
[SuppressMessage("Design", "CA1032:Implement standard exception constructors", Justification = "It always carries an error.")]
public sealed class ToolErrorException : McpException
{
    /// <summary>Initializes a new instance of the <see cref="ToolErrorException"/> class.</summary>
    /// <param name="error">The failure.</param>
    public ToolErrorException(Error error)
        : base(Describe(error))
    {
        Error = error;
    }

    /// <summary>Gets the failure.</summary>
    public Error Error { get; }

    private static string Describe(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return $"{error.Code}: {error.Message}";
    }
}
