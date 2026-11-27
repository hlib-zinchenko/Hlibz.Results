namespace Hlibz.Results.Mcp;

/// <summary>
/// Works out errors a tool can return that its attributes don't declare, such as those of the use
/// case it runs. Register it as a service; its errors follow the tool's
/// <see cref="ToolErrorsAttribute"/>s, each listed once.
/// </summary>
public interface IToolErrorSource
{
    /// <summary>Gets the errors <paramref name="tool"/> can return.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>Error types (subtypes of <see cref="Error"/>); none when it knows nothing of the tool.</returns>
    IEnumerable<Type> ErrorsOf(McpServerTool tool);
}
