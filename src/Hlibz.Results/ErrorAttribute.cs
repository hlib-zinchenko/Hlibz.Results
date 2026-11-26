namespace Hlibz.Results;

/// <summary>
/// Describes an <see cref="Error"/> type: the code clients branch on, what failed and the message
/// that goes with it. Constant, so documentation and the error code catalog know them without
/// creating an error.
/// </summary>
/// <param name="code">The code, e.g. <c>cities_1002</c>. Unique per application.</param>
/// <param name="status">What failed: not <see cref="ResultStatus.Success"/>.</param>
/// <param name="message">A human-readable description of the failure.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ErrorAttribute(string code, ResultStatus status, string message) : Attribute
{
    /// <summary>
    /// Gets the code clients branch on.
    /// </summary>
    public string Code { get; } = code;

    /// <summary>
    /// Gets what failed.
    /// </summary>
    public ResultStatus Status { get; } = status;

    /// <summary>
    /// Gets a human-readable description of the failure.
    /// </summary>
    public string Message { get; } = message;
}
