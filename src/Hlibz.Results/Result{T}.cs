namespace Hlibz.Results;

/// <summary>
/// The outcome of an operation that returns a <typeparamref name="T"/> on success, or fails with
/// any <see cref="Results.Error"/>. Return the value or an error where it is expected: both
/// convert. A <see cref="Result{T}"/> is a <see cref="Result"/>, so it can be passed on wherever a
/// non-generic result is expected. To declare which errors an operation returns, use
/// <see cref="Result{T, TError}"/>.
/// </summary>
/// <typeparam name="T">The type of the value on success.</typeparam>
[SuppressMessage(
    "Design",
    "CA1000:Do not declare static members on generic types",
    Justification = "Result<T>.Success(value) is the point of the type.")]
public class Result<T> : Result
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Result{T}"/> class.
    /// </summary>
    /// <param name="value">The value on success, <see langword="default"/> otherwise.</param>
    /// <param name="error">The failure, or <see langword="null"/> for a success.</param>
    private protected Result(T? value, Error? error)
        : base(error)
    {
        Value = value;
    }

    /// <summary>
    /// Gets the value: set on success, <see langword="default"/> on failure. A success holds
    /// <see langword="null"/> only when <typeparamref name="T"/> is itself nullable
    /// (<c>Result&lt;OrderDto?&gt;</c>).
    /// </summary>
    public T? Value { get; }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded; when it did,
    /// <see cref="Value"/> isn't <see langword="null"/>, and when it didn't,
    /// <see cref="Result.Error"/> isn't.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public override bool IsSuccess => base.IsSuccess;

    /// <summary>
    /// Gets a value indicating whether the operation failed; when it didn't,
    /// <see cref="Value"/> isn't <see langword="null"/>, and when it did,
    /// <see cref="Result.Error"/> isn't.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    public override bool IsFailure => base.IsFailure;

    /// <summary>
    /// Succeeds with <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value.</param>
    [SuppressMessage(
        "Usage",
        "CA2225:Operator overloads have named alternates",
        Justification = "Success is the named alternate.")]
    public static implicit operator Result<T>(T value) => Success(value);

    /// <summary>
    /// Fails with <paramref name="error"/>.
    /// </summary>
    /// <param name="error">The failure.</param>
    [SuppressMessage(
        "Usage",
        "CA2225:Operator overloads have named alternates",
        Justification = "Failure is the named alternate.")]
    public static implicit operator Result<T>(Error error) => Failure(error);

    /// <summary>
    /// Creates a successful result holding <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value; <see langword="null"/> only for a nullable
    /// <typeparamref name="T"/>.</param>
    /// <returns>The successful result.</returns>
    public static Result<T> Success(T value) => new(value, null);

    /// <inheritdoc cref="Result.Failure(Results.Error)" />
    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(default, error);
    }

    /// <inheritdoc />
    public override string ToString() =>
        IsSuccess ? $"{nameof(ResultStatus.Success)}: {Value}" : base.ToString();

    /// <inheritdoc/>
    internal override TOut Accept<TOut>(IResultVisitor<TOut> visitor) => visitor.Visit(this);
}
