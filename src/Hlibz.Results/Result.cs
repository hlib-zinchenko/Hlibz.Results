namespace Hlibz.Results;

/// <summary>
/// The outcome of an operation that returns no value: success, or a failure carrying an
/// <see cref="Results.Error"/>. Return an error where a result is expected: it converts.
/// </summary>
public class Result
{
    private static readonly Result SuccessResult = new(null);

    /// <summary>
    /// Initializes a new instance of the <see cref="Result"/> class.
    /// </summary>
    /// <param name="error">The failure, or <see langword="null"/> for a success.</param>
    private protected Result(Error? error)
    {
        Error = error;
    }

    /// <summary>
    /// Gets the failure, or <see langword="null"/> on success.
    /// </summary>
    public Error? Error { get; }

    /// <summary>
    /// Gets the outcome: <see cref="ResultStatus.Success"/>, or the error's status.
    /// </summary>
    public ResultStatus Status => Error?.Status ?? ResultStatus.Success;

    /// <summary>
    /// Gets a value indicating whether the operation succeeded; when it didn't,
    /// <see cref="Error"/> isn't <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Error))]
    public virtual bool IsSuccess => Error is null;

    /// <summary>
    /// Gets a value indicating whether the operation failed; when it did, <see cref="Error"/>
    /// isn't <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public virtual bool IsFailure => Error is not null;

    /// <summary>
    /// Fails with <paramref name="error"/>.
    /// </summary>
    /// <param name="error">The failure.</param>
    [SuppressMessage(
        "Usage",
        "CA2225:Operator overloads have named alternates",
        Justification = "Failure is the named alternate.")]
    public static implicit operator Result(Error error) => Failure(error);

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>The successful result.</returns>
    public static Result Success() => SuccessResult;

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="error">The failure.</param>
    /// <returns>The failed result.</returns>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    /// <inheritdoc />
    public override string ToString() =>
        Error is null ? nameof(ResultStatus.Success) : $"{Error.Status}: {Error.Message} [{Error.Code}]";

    /// <summary>
    /// Calls <paramref name="visitor"/>'s overload for this result's type:
    /// <see cref="IResultVisitor{TOut}.Visit(Result)"/> here, the generic one for a
    /// <see cref="Result{T}"/>.
    /// </summary>
    /// <typeparam name="TOut">What the visit returns.</typeparam>
    /// <param name="visitor">The visitor.</param>
    /// <returns>What the visit returned.</returns>
    internal virtual TOut Accept<TOut>(IResultVisitor<TOut> visitor) => visitor.Visit(this);
}
