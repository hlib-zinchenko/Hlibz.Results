namespace Hlibz.Results.Mcp;

/// <summary>
/// Declares that a tool can return <typeparamref name="TError"/>: an ambient error such as
/// <see cref="ValidationFailed"/> or <see cref="Forbidden"/> that no error set lists, or an error of
/// a tool that unwraps a plain <see cref="Result{T}"/>.
/// </summary>
/// <typeparam name="TError">The error.</typeparam>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ToolErrorAttribute<TError> : ToolErrorsAttribute
    where TError : Error
{
    /// <inheritdoc />
    public override IReadOnlyList<Type> ErrorTypes { get; } = [typeof(TError)];
}
