using FluentValidation.Results;

namespace Hlibz.Results.FluentValidation;

/// <summary>
/// Converts a failed <see cref="ValidationResult"/> into a failure with the status your API
/// wants: <see cref="ToErrorResult"/> (<see cref="ResultStatus.Error"/>, for APIs that treat
/// validation like any other bad request) or <see cref="ToInvalidResult"/>
/// (<see cref="ResultStatus.Invalid"/>, for APIs that tell them apart, e.g. 400 vs 422). Errors
/// are keyed by property path as FluentValidation reports it (e.g. <c>Items[0].Quantity</c>);
/// the HTTP layer decides how keys are cased.
/// </summary>
/// <remarks>
/// Plain extension methods rather than C# 14 static extensions on <see cref="Result"/>: they
/// work with any C# version, and analyzers such as BannedApiAnalyzers can ban one of them.
/// </remarks>
public static class ValidationResultExtensions
{
    /// <summary>
    /// Converts a failed validation into a <see cref="ResultStatus.Error"/> failure carrying its
    /// validation errors.
    /// </summary>
    /// <param name="validationResult">The failed validation.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="InvalidOperationException">The validation passed.</exception>
    public static Result ToErrorResult(this ValidationResult validationResult) =>
        Result.Error(ToErrors(validationResult));

    /// <summary>
    /// Converts a failed validation into a <see cref="ResultStatus.Error"/> failure carrying its
    /// validation errors, typed as <see cref="Result{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value the result would hold on success.</typeparam>
    /// <param name="validationResult">The failed validation.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="InvalidOperationException">The validation passed.</exception>
    public static Result<T> ToErrorResult<T>(this ValidationResult validationResult) =>
        Result<T>.Error(ToErrors(validationResult));

    /// <summary>
    /// Converts a failed validation into a <see cref="ResultStatus.Invalid"/> failure.
    /// </summary>
    /// <param name="validationResult">The failed validation.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="InvalidOperationException">The validation passed.</exception>
    public static Result ToInvalidResult(this ValidationResult validationResult) =>
        Result.Invalid(ToErrors(validationResult));

    /// <summary>
    /// Converts a failed validation into a <see cref="ResultStatus.Invalid"/> failure typed as
    /// <see cref="Result{T}"/>.
    /// </summary>
    /// <typeparam name="T">The type of the value the result would hold on success.</typeparam>
    /// <param name="validationResult">The failed validation.</param>
    /// <returns>The failed result.</returns>
    /// <exception cref="InvalidOperationException">The validation passed.</exception>
    public static Result<T> ToInvalidResult<T>(this ValidationResult validationResult) =>
        Result<T>.Invalid(ToErrors(validationResult));

    /// <summary>
    /// Groups the validation failures' messages by property path.
    /// </summary>
    /// <param name="validationResult">The failed validation.</param>
    /// <returns>The messages keyed by property path, in the order they were reported.</returns>
    /// <exception cref="InvalidOperationException">The validation passed.</exception>
    public static IReadOnlyDictionary<string, string[]> ToErrors(
        this ValidationResult validationResult)
    {
        ArgumentNullException.ThrowIfNull(validationResult);
        if (validationResult.IsValid)
        {
            throw new InvalidOperationException(
                "Cannot create an error result from a valid validation result.");
        }

        return validationResult
            .Errors
            .GroupBy(failure => failure.PropertyName, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
    }
}
