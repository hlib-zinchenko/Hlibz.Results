using System.Runtime.CompilerServices;

namespace Hlibz.Results;

/// <summary>
/// The outcome of an operation that returns a <typeparamref name="T"/> on success, and declares
/// the errors it fails with: <typeparamref name="TError"/>, a <see langword="union"/> of error
/// types (an error set). It can also fail with an <see cref="AmbientError"/>, which the layers
/// around an operation add. Return the value, an error of the set or an ambient error where it is
/// expected: each converts. Documentation reads the set, so each error is documented with its
/// data.
/// </summary>
/// <typeparam name="T">The type of the value on success; <see cref="Unit"/> for none.</typeparam>
/// <typeparam name="TError">The error set: a non-generic <see langword="union"/> whose cases are
/// all error types, none of them ambient.</typeparam>
/// <example>
/// <code>
/// public union DeleteCityError(CityNotFound, CityInUse);
///
/// public Result&lt;Unit, DeleteCityError&gt; Delete(Guid id) =>
///     usedBy.Count > 0 ? new CityInUse(id, usedBy.Count) : Unit.Value;
///
/// string text = result.DeclaredError switch
/// {
///     CityNotFound => "no such city",
///     CityInUse inUse => $"used by {inUse.UserCount} users",
///     null => "succeeded, or an ambient error",
/// };
/// </code>
/// </example>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Result<T, TError>.Success(value) is the point of the type.")]
public sealed class Result<T, TError> : Result<T>
    where TError : struct, IUnion
{
    private readonly object? _declaredError;

    private Result(T? value, Error? error, object? declaredError)
        : base(value, error)
    {
        _declaredError = declaredError;
    }

    /// <summary>
    /// Gets the failure when it is one of the set's errors, for an exhaustive
    /// <see langword="switch"/> over them; <see langword="null"/> on success or for an
    /// <see cref="AmbientError"/>.
    /// </summary>
    public TError? DeclaredError => (TError?)_declaredError;

    /// <summary>
    /// Succeeds with <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    [SuppressMessage(
        "Usage",
        "CA2225:Operator overloads have named alternates",
        Justification = "Success is the named alternate.")]
    public static implicit operator Result<T, TError>(T value) => Success(value);

    /// <summary>
    /// Fails with <paramref name="error"/>: one of the set's errors, or an ambient one.
    /// </summary>
    /// <param name="error">The failure.</param>
    /// <exception cref="ArgumentException">The error is neither in the set nor ambient (the
    /// build reports this as HRES005 where it can tell).</exception>
    [SuppressMessage(
        "Usage",
        "CA2225:Operator overloads have named alternates",
        Justification = "Failure is the named alternate.")]
    public static implicit operator Result<T, TError>(Error error) => Failure(error);

    /// <inheritdoc cref="Result{T}.Success(T)" />
    public static new Result<T, TError> Success(T value) => new(value, null, null);

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The failure: one of the set's errors, or an ambient one.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="ArgumentException">The error is neither in the set nor ambient (the
    /// build reports this as HRES005 where it can tell).</exception>
    public static new Result<T, TError> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error is AmbientError)
        {
            return new Result<T, TError>(default, error, null);
        }

        object declared = ErrorSetDescriptor.For(typeof(TError)).Wrap(error)
            ?? throw new ArgumentException(
                $"{error.GetType().Name} isn't one of {typeof(TError).Name}'s errors, nor an "
                + $"{nameof(AmbientError)}: add it to the union, or return one of its errors.",
                nameof(error));
        return new Result<T, TError>(default, error, declared);
    }
}
