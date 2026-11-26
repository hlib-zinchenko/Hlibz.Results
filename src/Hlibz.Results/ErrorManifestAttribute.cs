using System.ComponentModel;

namespace Hlibz.Results;

/// <summary>
/// The error types and error sets of an assembly, written into it by the package's source
/// generator when it compiles, so they are read from a list rather than found by scanning types.
/// Not for use in your own code.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false, Inherited = false)]
[EditorBrowsable(EditorBrowsableState.Never)]
public abstract class ErrorManifestAttribute : Attribute
{
    /// <summary>
    /// Gets the assembly's error types.
    /// </summary>
    public abstract IReadOnlyList<ErrorDescriptor> Errors { get; }

    /// <summary>
    /// Gets the assembly's error sets.
    /// </summary>
    public abstract IReadOnlyList<ErrorSetDescriptor> ErrorSets { get; }
}
