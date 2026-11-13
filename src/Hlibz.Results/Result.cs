namespace Hlibz.Results;

/// <summary>
/// The outcome of an operation that returns no value: success, or a failure with a
/// <see cref="ResultStatus"/>, an optional message, error code and validation errors.
/// </summary>
public class Result
{
    /// <summary>
    /// The message of a failure carrying validation errors when none is given.
    /// </summary>
    public const string DefaultValidationMessage =
        "Validation failed. Please check the validation errors.";

    private static readonly Result SuccessResult = new(ResultStatus.Success);

    /// <summary>
    /// Initializes a new instance of the <see cref="Result"/> class.
    /// </summary>
    /// <param name="status">The outcome.</param>
    /// <param name="errorMessage">The failure's message, if any.</param>
    /// <param name="errorCode">The failure's machine-readable code, if any.</param>
    /// <param name="validationErrors">The failure's validation errors, if any.</param>
    protected Result(
        ResultStatus status,
        string? errorMessage = null,
        string? errorCode = null,
        IReadOnlyDictionary<string, string[]>? validationErrors = null)
    {
        Status = status;
        ErrorMessage = errorMessage;
        ErrorCode = errorCode;
        ValidationErrors = validationErrors;
    }

    /// <summary>
    /// Gets the outcome.
    /// </summary>
    public ResultStatus Status { get; }

    /// <summary>
    /// Gets the failure's message, or <see langword="null"/> on success or when the failure has
    /// none (the HTTP layer then uses a default per status).
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets the failure's machine-readable code (<see cref="Results.ErrorCode.Code"/>), or
    /// <see langword="null"/> when it has none.
    /// </summary>
    public string? ErrorCode { get; }

    /// <summary>
    /// Gets the failure's validation errors keyed by the offending property path as given (e.g.
    /// <c>Items[0].Quantity</c>), or <see langword="null"/>.
    /// </summary>
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public virtual bool IsSuccess => Status == ResultStatus.Success;

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public virtual bool IsFailure => !IsSuccess;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <returns>The successful result.</returns>
    public static Result Success() => SuccessResult;

    /// <summary>
    /// Creates an <see cref="ResultStatus.Error"/> failure.
    /// </summary>
    /// <param name="errorMessage">The failure's message.</param>
    /// <returns>The failed result.</returns>
    public static Result Error(string errorMessage) => new(ResultStatus.Error, errorMessage);

    /// <summary>
    /// Creates an <see cref="ResultStatus.Error"/> failure with an error code.
    /// </summary>
    /// <param name="errorCode">The error code, whose message becomes the failure's.</param>
    /// <returns>The failed result.</returns>
    public static Result Error(ErrorCode errorCode) => Fail(ResultStatus.Error, errorCode);

    /// <summary>
    /// Creates an <see cref="ResultStatus.Error"/> failure carrying validation errors, for APIs
    /// that don't tell validation apart from other bad requests (see <see cref="Invalid"/>).
    /// </summary>
    /// <param name="validationErrors">The errors keyed by property path; must not be empty.</param>
    /// <param name="errorMessage">The failure's message, <see cref="DefaultValidationMessage"/>
    /// when <see langword="null"/>.</param>
    /// <returns>The failed result.</returns>
    public static Result Error(
        IReadOnlyDictionary<string, string[]> validationErrors,
        string? errorMessage = null) =>
        new(
            ResultStatus.Error,
            errorMessage ?? DefaultValidationMessage,
            validationErrors: RequireErrors(validationErrors));

    /// <summary>
    /// Creates an <see cref="ResultStatus.Invalid"/> failure carrying validation errors.
    /// </summary>
    /// <param name="validationErrors">The errors keyed by property path; must not be empty.</param>
    /// <param name="errorMessage">The failure's message, <see cref="DefaultValidationMessage"/>
    /// when <see langword="null"/>.</param>
    /// <returns>The failed result.</returns>
    public static Result Invalid(
        IReadOnlyDictionary<string, string[]> validationErrors,
        string? errorMessage = null) =>
        new(
            ResultStatus.Invalid,
            errorMessage ?? DefaultValidationMessage,
            validationErrors: RequireErrors(validationErrors));

    /// <summary>
    /// Creates a <see cref="ResultStatus.NotFound"/> failure.
    /// </summary>
    /// <param name="errorMessage">The failure's message, if any.</param>
    /// <returns>The failed result.</returns>
    public static Result NotFound(string? errorMessage = null) =>
        new(ResultStatus.NotFound, errorMessage);

    /// <summary>
    /// Creates a <see cref="ResultStatus.NotFound"/> failure with an error code.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>The failed result.</returns>
    public static Result NotFound(ErrorCode errorCode) => Fail(ResultStatus.NotFound, errorCode);

    /// <summary>
    /// Creates an <see cref="ResultStatus.Unauthorized"/> failure.
    /// </summary>
    /// <param name="errorMessage">The failure's message, if any.</param>
    /// <returns>The failed result.</returns>
    public static Result Unauthorized(string? errorMessage = null) =>
        new(ResultStatus.Unauthorized, errorMessage);

    /// <summary>
    /// Creates an <see cref="ResultStatus.Unauthorized"/> failure with an error code.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>The failed result.</returns>
    public static Result Unauthorized(ErrorCode errorCode) =>
        Fail(ResultStatus.Unauthorized, errorCode);

    /// <summary>
    /// Creates a <see cref="ResultStatus.Forbidden"/> failure.
    /// </summary>
    /// <param name="errorMessage">The failure's message, if any.</param>
    /// <returns>The failed result.</returns>
    public static Result Forbidden(string? errorMessage = null) =>
        new(ResultStatus.Forbidden, errorMessage);

    /// <summary>
    /// Creates a <see cref="ResultStatus.Forbidden"/> failure with an error code.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>The failed result.</returns>
    public static Result Forbidden(ErrorCode errorCode) => Fail(ResultStatus.Forbidden, errorCode);

    /// <summary>
    /// Creates a <see cref="ResultStatus.Conflict"/> failure.
    /// </summary>
    /// <param name="errorMessage">The failure's message, if any.</param>
    /// <returns>The failed result.</returns>
    public static Result Conflict(string? errorMessage = null) =>
        new(ResultStatus.Conflict, errorMessage);

    /// <summary>
    /// Creates a <see cref="ResultStatus.Conflict"/> failure with an error code.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>The failed result.</returns>
    public static Result Conflict(ErrorCode errorCode) => Fail(ResultStatus.Conflict, errorCode);

    /// <summary>
    /// Creates an <see cref="ResultStatus.Unavailable"/> failure.
    /// </summary>
    /// <param name="errorMessage">The failure's message, if any.</param>
    /// <returns>The failed result.</returns>
    public static Result Unavailable(string? errorMessage = null) =>
        new(ResultStatus.Unavailable, errorMessage);

    /// <summary>
    /// Creates an <see cref="ResultStatus.Unavailable"/> failure with an error code.
    /// </summary>
    /// <param name="errorCode">The error code.</param>
    /// <returns>The failed result.</returns>
    public static Result Unavailable(ErrorCode errorCode) =>
        Fail(ResultStatus.Unavailable, errorCode);

    /// <summary>
    /// Copies a failure (status, message, code, validation errors) into a non-generic result,
    /// e.g. to pass on a failed <see cref="Result{T}"/>.
    /// </summary>
    /// <param name="result">The failed result.</param>
    /// <returns>The same failure without a value.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="result"/> succeeded.</exception>
    public static Result FromFailed(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        EnsureFailed(result);
        return new Result(
            result.Status,
            result.ErrorMessage,
            result.ErrorCode,
            result.ValidationErrors);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        if (IsSuccess)
        {
            return nameof(ResultStatus.Success);
        }

        string message = ErrorMessage is null ? Status.ToString() : $"{Status}: {ErrorMessage}";
        return ErrorCode is null ? message : $"{message} [{ErrorCode}]";
    }

    /// <summary>
    /// Calls <paramref name="visitor"/>'s overload for this result's type:
    /// <see cref="IResultVisitor{TOut}.Visit(Result)"/> here, the generic one for a
    /// <see cref="Result{T}"/>.
    /// </summary>
    /// <typeparam name="TOut">What the visit returns.</typeparam>
    /// <param name="visitor">The visitor.</param>
    /// <returns>What the visit returned.</returns>
    internal virtual TOut Accept<TOut>(IResultVisitor<TOut> visitor) => visitor.Visit(this);

    /// <summary>
    /// Throws unless <paramref name="result"/> is a failure.
    /// </summary>
    /// <param name="result">The result to check.</param>
    /// <exception cref="InvalidOperationException"><paramref name="result"/> succeeded.</exception>
    protected static void EnsureFailed(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException(
                "Cannot create a failed result from a successful result.");
        }
    }

    /// <summary>
    /// Throws unless there is at least one validation error.
    /// </summary>
    /// <param name="validationErrors">The validation errors.</param>
    /// <returns>The same <paramref name="validationErrors"/>.</returns>
    protected static IReadOnlyDictionary<string, string[]> RequireErrors(
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        ArgumentNullException.ThrowIfNull(validationErrors);
        if (validationErrors.Count == 0)
        {
            throw new ArgumentException(
                "A validation failure needs at least one validation error.",
                nameof(validationErrors));
        }

        return validationErrors;
    }

    private static Result Fail(ResultStatus status, ErrorCode errorCode)
    {
        ArgumentNullException.ThrowIfNull(errorCode);
        return new Result(status, errorCode.Message, errorCode.Code);
    }
}
