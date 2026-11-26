namespace Hlibz.Results;

/// <summary>
/// No value: the success of an operation that returns nothing but declares its errors,
/// <c>Result&lt;Unit, TError&gt;</c>. Sent like a plain <see cref="Result"/>'s success, with no
/// body.
/// </summary>
public readonly record struct Unit
{
    /// <summary>
    /// Gets the one value.
    /// </summary>
    public static Unit Value => default;
}
