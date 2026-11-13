namespace Hlibz.Results;

/// <summary>
/// The outcome of an operation that returns a <typeparamref name="T"/> on success. A
/// <see cref="Result{T}"/> is a <see cref="Result"/>, so it can be passed on wherever a
/// non-generic result is expected.
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
    /// <param name="status">The outcome.</param>
    /// <param name="errorMessage">The failure's message, if any.</param>
    /// <param name="errorCode">The failure's machine-readable code, if any.</param>
    /// <param name="validationErrors">The failure's validation errors, if any.</param>
    protected Result(
        T? value,
        ResultStatus status,
        string? errorMessage = null,
        string? errorCode = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
        : base(status, errorMessage, errorCode, validationErrors)
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
    /// <see cref="Value"/> isn't <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    public override bool IsSuccess => base.IsSuccess;

    /// <summary>
    /// Gets a value indicating whether the operation failed; when it didn't,
    /// <see cref="Value"/> isn't <see langword="null"/>.
    /// </summary>
    [MemberNotNullWhen(false, nameof(Value))]
    public override bool IsFailure => base.IsFailure;

    /// <summary>
    /// Creates a successful result holding <paramref name="value"/>.
    /// </summary>
    /// <param name="value">The value; <see langword="null"/> only for a nullable
    /// <typeparamref name="T"/>.</param>
    /// <returns>The successful result.</returns>
    public static Result<T> Success(T value) => new(value, ResultStatus.Success);

    /// <inheritdoc cref="Result.Error(string)" />
    public static new Result<T> Error(string errorMessage) =>
        new(default, ResultStatus.Error, errorMessage);

    /// <inheritdoc cref="Result.Error(Hlibz.Results.ErrorCode)" />
    public static new Result<T> Error(ErrorCode errorCode) => Fail(ResultStatus.Error, errorCode);

    /// <inheritdoc cref="Result.Error(IReadOnlyDictionary{string, string[]}, string)" />
    public static new Result<T> Error(
        IReadOnlyDictionary<string, string[]> validationErrors,
        string? errorMessage = null) =>
        new(
            default,
            ResultStatus.Error,
            errorMessage ?? DefaultValidationMessage,
            validationErrors: RequireErrors(validationErrors));

    /// <inheritdoc cref="Result.Invalid" />
    public static new Result<T> Invalid(
        IReadOnlyDictionary<string, string[]> validationErrors,
        string? errorMessage = null) =>
        new(
            default,
            ResultStatus.Invalid,
            errorMessage ?? DefaultValidationMessage,
            validationErrors: RequireErrors(validationErrors));

    /// <inheritdoc cref="Result.NotFound(string)" />
    public static new Result<T> NotFound(string? errorMessage = null) =>
        new(default, ResultStatus.NotFound, errorMessage);

    /// <inheritdoc cref="Result.NotFound(Hlibz.Results.ErrorCode)" />
    public static new Result<T> NotFound(ErrorCode errorCode) =>
        Fail(ResultStatus.NotFound, errorCode);

    /// <inheritdoc cref="Result.Unauthorized(string)" />
    public static new Result<T> Unauthorized(string? errorMessage = null) =>
        new(default, ResultStatus.Unauthorized, errorMessage);

    /// <inheritdoc cref="Result.Unauthorized(Hlibz.Results.ErrorCode)" />
    public static new Result<T> Unauthorized(ErrorCode errorCode) =>
        Fail(ResultStatus.Unauthorized, errorCode);

    /// <inheritdoc cref="Result.Forbidden(string)" />
    public static new Result<T> Forbidden(string? errorMessage = null) =>
        new(default, ResultStatus.Forbidden, errorMessage);

    /// <inheritdoc cref="Result.Forbidden(Hlibz.Results.ErrorCode)" />
    public static new Result<T> Forbidden(ErrorCode errorCode) =>
        Fail(ResultStatus.Forbidden, errorCode);

    /// <inheritdoc cref="Result.Conflict(string)" />
    public static new Result<T> Conflict(string? errorMessage = null) =>
        new(default, ResultStatus.Conflict, errorMessage);

    /// <inheritdoc cref="Result.Conflict(Hlibz.Results.ErrorCode)" />
    public static new Result<T> Conflict(ErrorCode errorCode) =>
        Fail(ResultStatus.Conflict, errorCode);

    /// <inheritdoc cref="Result.Unavailable(string)" />
    public static new Result<T> Unavailable(string? errorMessage = null) =>
        new(default, ResultStatus.Unavailable, errorMessage);

    /// <inheritdoc cref="Result.Unavailable(Hlibz.Results.ErrorCode)" />
    public static new Result<T> Unavailable(ErrorCode errorCode) =>
        Fail(ResultStatus.Unavailable, errorCode);

    /// <summary>
    /// Copies a failure (status, message, code, validation errors) of any result into a
    /// <see cref="Result{T}"/>, to pass it on from a method returning a different type.
    /// </summary>
    /// <param name="result">The failed result.</param>
    /// <returns>The same failure, typed as <see cref="Result{T}"/>.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="result"/> succeeded.</exception>
    public static new Result<T> FromFailed(Result result)
    {
        EnsureFailed(result);
        return new Result<T>(
            default,
            result.Status,
            result.ErrorMessage,
            result.ErrorCode,
            result.ValidationErrors);
    }

    /// <inheritdoc />
    public override string ToString() =>
        IsSuccess ? $"{nameof(ResultStatus.Success)}: {Value}" : base.ToString();

    /// <inheritdoc/>
    internal override TOut Accept<TOut>(IResultVisitor<TOut> visitor) => visitor.Visit(this);

    private static Result<T> Fail(ResultStatus status, ErrorCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        return new Result<T>(default, status, errorCode.Message, errorCode.Code);
    }
}
