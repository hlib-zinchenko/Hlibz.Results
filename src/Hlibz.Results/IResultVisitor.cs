namespace Hlibz.Results;

/// <summary>
/// Does something with a result knowing its value type, without reflection: a
/// <see cref="Result{T}"/> calls <see cref="Visit{T}"/> with its own <c>T</c>, so code holding a
/// plain <see cref="Result"/> reaches the typed overloads in a way Native AOT compiles ahead of
/// time. For Hlibz.Results.AspNetCore.
/// </summary>
/// <typeparam name="TOut">What the visit returns.</typeparam>
internal interface IResultVisitor<out TOut>
{
    /// <summary>Visits a result that holds no value.</summary>
    /// <param name="result">The result.</param>
    /// <returns>What the visit returns.</returns>
    TOut Visit(Result result);

    /// <summary>Visits a result that holds a <typeparamref name="T"/> on success.</summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>What the visit returns.</returns>
    TOut Visit<T>(Result<T> result);
}
