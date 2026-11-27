namespace Hlibz.Results.Mcp;

/// <summary>
/// Declares errors a tool can return, for its <c>_meta</c> (<see cref="ToolErrorOptions.MetaKey"/>)
/// and the start-up check that each has guidance. Put
/// <see cref="ToolErrorsAttribute{TErrorSet}"/> (an error set) or <see cref="ToolErrorAttribute{TError}"/>
/// (one error, such as <see cref="ValidationFailed"/>) on the tool's method, as many as it needs; an
/// <see cref="IToolErrorSource"/> can add more.
/// </summary>
public abstract class ToolErrorsAttribute : Attribute
{
    /// <summary>Gets the error types declared, in order.</summary>
    public abstract IReadOnlyList<Type> ErrorTypes { get; }
}
