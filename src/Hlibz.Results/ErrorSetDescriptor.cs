using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;

namespace Hlibz.Results;

/// <summary>
/// An error set: a <see langword="union"/> of <see cref="Error"/> types, the errors an operation
/// returning a <see cref="Result{T, TError}"/> declares. Read from the list the package's source
/// generator writes into each assembly.
/// </summary>
public sealed class ErrorSetDescriptor
{
    private static readonly ConcurrentDictionary<Assembly, IReadOnlyDictionary<Type, ErrorSetDescriptor>> Manifests = new();

    private readonly Func<Error, object?> _wrap;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorSetDescriptor"/> class. Written by the
    /// source generator; not for use in your own code.
    /// </summary>
    /// <param name="type">The union type.</param>
    /// <param name="errorTypes">Its case types.</param>
    /// <param name="wrap">Wraps an error of one of the case types in the union (boxed), or
    /// returns <see langword="null"/> for any other error.</param>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public ErrorSetDescriptor(Type type, IReadOnlyList<Type> errorTypes, Func<Error, object?> wrap)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentNullException.ThrowIfNull(errorTypes);
        ArgumentNullException.ThrowIfNull(wrap);
        Type = type;
        ErrorTypes = errorTypes;
        _wrap = wrap;
    }

    /// <summary>
    /// Gets the union type.
    /// </summary>
    public Type Type { get; }

    /// <summary>
    /// Gets the error types the set lists, in declaration order.
    /// </summary>
    public IReadOnlyList<Type> ErrorTypes { get; }

    /// <summary>
    /// Gets the description of <paramref name="setType"/>.
    /// </summary>
    /// <param name="setType">A <see langword="union"/> of error types.</param>
    /// <returns>Its description.</returns>
    /// <exception cref="InvalidOperationException">The generated list of its assembly doesn't
    /// have it: a case isn't an error type, or the generator didn't run.</exception>
    public static ErrorSetDescriptor For(Type setType)
    {
        ArgumentNullException.ThrowIfNull(setType);
        IReadOnlyDictionary<Type, ErrorSetDescriptor> sets = Manifests.GetOrAdd(
            setType.Assembly,
            static assembly => assembly.GetCustomAttribute<ErrorManifestAttribute>()?.ErrorSets
                .ToDictionary(descriptor => descriptor.Type) ?? []);
        return sets.TryGetValue(setType, out ErrorSetDescriptor? descriptor)
            ? descriptor
            : throw new InvalidOperationException(
                $"{setType.FullName} isn't an error set: declare it as a non-generic union whose "
                + "cases are all error types (none of them an AmbientError), and compile its "
                + "project with the Hlibz.Results source generator.");
    }

    /// <summary>
    /// Wraps <paramref name="error"/> in the union, boxed.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The union holding it, or <see langword="null"/> when the set doesn't list its
    /// type.</returns>
    internal object? Wrap(Error error) => _wrap(error);
}
