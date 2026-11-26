using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Hlibz.Results;

/// <summary>
/// An <see cref="Error"/> type as its <see cref="ErrorAttribute"/> describes it, read from the
/// list the package's source generator writes into each assembly.
/// </summary>
public sealed class ErrorDescriptor
{
    private static readonly ConcurrentDictionary<Assembly, IReadOnlyDictionary<Type, ErrorDescriptor>> Manifests = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorDescriptor"/> class. Written by the
    /// source generator; not for use in your own code.
    /// </summary>
    /// <param name="type">The error type.</param>
    /// <param name="code">Its code.</param>
    /// <param name="status">What failed.</param>
    /// <param name="message">Its message.</param>
    /// <param name="hasDetails">Whether it carries data of its own.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ErrorDescriptor(Type type, string code, ResultStatus status, string message, bool hasDetails)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentNullException.ThrowIfNull(message);
        if (status == ResultStatus.Success)
        {
            throw new ArgumentException($"{type.Name} is an error: its status can't be Success.", nameof(status));
        }

        Type = type;
        Code = code;
        Status = status;
        Message = message;
        HasDetails = hasDetails;
    }

    /// <summary>
    /// Gets the error type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the code clients branch on.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets what failed.
    /// </summary>
    public ResultStatus Status { get; }

    /// <summary>
    /// Gets the message, as an error words it unless it overrides <c>FormatMessage</c>.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets a value indicating whether the error carries data of its own (public instance
    /// properties besides <see cref="Error"/>'s), which goes out with it as its details.
    /// </summary>
    public bool HasDetails { get; }

    /// <summary>
    /// Gets the description of <paramref name="errorType"/>.
    /// </summary>
    /// <param name="errorType">A concrete <see cref="Error"/> type.</param>
    /// <returns>Its description.</returns>
    /// <exception cref="InvalidOperationException">The generated list of its assembly doesn't
    /// have it: it has no <see cref="ErrorAttribute"/> or the generator didn't run.</exception>
    public static ErrorDescriptor For(Type errorType)
    {
        ArgumentNullException.ThrowIfNull(errorType);
        return In(errorType.Assembly).TryGetValue(errorType, out ErrorDescriptor? descriptor)
            ? descriptor
            : throw new InvalidOperationException(
                $"{errorType.FullName} isn't a described error: give it an [Error] attribute, and "
                + "compile its project with the Hlibz.Results source generator (reference the "
                + "package, and don't exclude its analyzers).");
    }

    /// <summary>
    /// The errors the source generator listed in <paramref name="assembly"/>; none when it has no
    /// list.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>Its errors by type.</returns>
    internal static IReadOnlyDictionary<Type, ErrorDescriptor> In(Assembly assembly) =>
        Manifests.GetOrAdd(
            assembly,
            static assembly => assembly.GetCustomAttribute<ErrorManifestAttribute>()?.Errors
                .ToDictionary(descriptor => descriptor.Type) ?? []);
}
