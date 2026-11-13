using System.ComponentModel;

namespace Hlibz.Results;

/// <summary>
/// The error codes of an assembly, written into it by the package's source generator when it
/// compiles, so <see cref="ErrorCodeCatalog"/> reads a list rather than scanning every type. Not
/// for use in your own code.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class ErrorCodeManifestAttribute : Attribute
{
    /// <summary>
    /// Gets a reader for each public static <see cref="ErrorCode"/> field and property.
    /// </summary>
    public abstract IReadOnlyList<Func<ErrorCode?>> ErrorCodes { get; }
}
