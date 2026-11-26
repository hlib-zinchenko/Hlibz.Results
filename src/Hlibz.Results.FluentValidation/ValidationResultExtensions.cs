using FluentValidation.Results;

namespace Hlibz.Results.FluentValidation;

/// <summary>
/// Converts a failed <see cref="ValidationResult"/> into a <see cref="ValidationFailed"/> error
/// (<see cref="ResultStatus.Invalid"/>: 400 by default, or whatever the app maps it to). Errors
/// are keyed by property path as FluentValidation reports it (e.g. <c>Items[0].Quantity</c>);
/// the HTTP layer decides how keys are cased.
/// </summary>
/// <remarks>
/// Plain extension methods rather than extension members: analyzers such as BannedApiAnalyzers
/// can ban one of them.
/// </remarks>
public static class ValidationResultExtensions
{
    /// <summary>
    /// Turns a failed validation into a <see cref="ValidationFailed"/> error, which converts to
    /// any result (<c>return validation.ToValidationFailed();</c>), whatever error set it declares.
    /// </summary>
    /// <param name="validationResult">The failed validation.</param>
    /// <returns>The error, keyed by property path as FluentValidation names them.</returns>
    /// <exception cref="InvalidOperationException">The validation passed.</exception>
    public static ValidationFailed ToValidationFailed(this ValidationResult validationResult) =>
        new(ToErrors(validationResult));

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
