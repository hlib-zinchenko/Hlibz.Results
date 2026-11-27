using System.Runtime.CompilerServices;

namespace Hlibz.Results.Mcp;

/// <summary>
/// Declares that a tool can return the errors of <typeparamref name="TErrorSet"/>: the error set of
/// the <c>Result&lt;T, TErrorSet&gt;</c> the tool unwraps.
/// </summary>
/// <typeparam name="TErrorSet">The error set: a <see langword="union"/> of error types.</typeparam>
/// <example>
/// <code>
/// [McpServerTool(Name = "delete_city")]
/// [ToolErrors&lt;DeleteCityError&gt;]
/// [ToolError&lt;ValidationFailed&gt;]
/// public static Unit Delete(CityService cities, Guid cityId) => cities.Delete(cityId).ValueOrThrow();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class ToolErrorsAttribute<TErrorSet> : ToolErrorsAttribute
    where TErrorSet : struct, IUnion
{
    /// <inheritdoc />
    /// <exception cref="InvalidOperationException"><typeparamref name="TErrorSet"/> isn't an error
    /// set (see <see cref="ErrorSetDescriptor.For"/>).</exception>
    public override IReadOnlyList<Type> ErrorTypes => ErrorSetDescriptor.For(typeof(TErrorSet)).ErrorTypes;
}
