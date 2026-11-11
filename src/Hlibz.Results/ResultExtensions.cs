namespace Hlibz.Results;

/// <summary>
/// Chains results: run the next step only when the previous one succeeded, passing any failure
/// on unchanged.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Transforms the value of a successful result; a failure is passed on unchanged.
    /// </summary>
    /// <typeparam name="TSource">The type of the value.</typeparam>
    /// <typeparam name="TDestination">The type of the transformed value.</typeparam>
    /// <param name="result">The result to transform.</param>
    /// <param name="map">The transformation, called only on success.</param>
    /// <returns>The transformed value, or the same failure.</returns>
    public static Result<TDestination> Map<TSource, TDestination>(
        this Result<TSource> result,
        Func<TSource, TDestination> map)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(map);
        return result.IsSuccess
            ? Result<TDestination>.Success(map(result.Value))
            : Result<TDestination>.FromFailed(result);
    }

    /// <summary>
    /// Transforms the value of a successful result once the task completes; a failure is passed
    /// on unchanged.
    /// </summary>
    /// <typeparam name="TSource">The type of the value.</typeparam>
    /// <typeparam name="TDestination">The type of the transformed value.</typeparam>
    /// <param name="resultTask">The pending result to transform.</param>
    /// <param name="map">The transformation, called only on success.</param>
    /// <returns>The transformed value, or the same failure.</returns>
    public static async Task<Result<TDestination>> MapAsync<TSource, TDestination>(
        this Task<Result<TSource>> resultTask,
        Func<TSource, TDestination> map)
    {
        ArgumentNullException.ThrowIfNull(resultTask);
        Result<TSource> result = await resultTask.ConfigureAwait(false);
        return result.Map(map);
    }

    /// <summary>
    /// Runs the next step on the value of a successful result; a failure is passed on unchanged.
    /// </summary>
    /// <typeparam name="TSource">The type of the value.</typeparam>
    /// <typeparam name="TDestination">The type of the next step's value.</typeparam>
    /// <param name="result">The result to continue from.</param>
    /// <param name="next">The next step, called only on success.</param>
    /// <returns>The next step's result, or the same failure.</returns>
    public static Result<TDestination> Bind<TSource, TDestination>(
        this Result<TSource> result,
        Func<TSource, Result<TDestination>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);
        return result.IsSuccess ? next(result.Value) : Result<TDestination>.FromFailed(result);
    }

    /// <summary>
    /// Runs the next step after a successful result; a failure is passed on unchanged.
    /// </summary>
    /// <typeparam name="TDestination">The type of the next step's value.</typeparam>
    /// <param name="result">The result to continue from.</param>
    /// <param name="next">The next step, called only on success.</param>
    /// <returns>The next step's result, or the same failure.</returns>
    public static Result<TDestination> Bind<TDestination>(
        this Result result,
        Func<Result<TDestination>> next)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(next);
        return result.IsSuccess ? next() : Result<TDestination>.FromFailed(result);
    }
}
