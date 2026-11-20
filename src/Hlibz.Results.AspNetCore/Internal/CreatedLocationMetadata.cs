namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// Endpoint metadata added by
/// <see cref="ResultEndpointConventionBuilderExtensions.WithCreatedLocation{T}"/>: a successful
/// <see cref="Result{T}"/> goes out as <c>201</c> with a <c>Location</c> made from its value.
/// </summary>
/// <param name="valueType">The value type the location is made from.</param>
/// <param name="location">Makes the location from a value of <paramref name="valueType"/>.</param>
internal sealed class CreatedLocationMetadata(Type valueType, Func<object?, string?> location)
{
    /// <summary>Gets the value type the location is made from.</summary>
    public Type ValueType { get; } = valueType;

    /// <summary>
    /// The location of <paramref name="value"/>, a successful <see cref="Result{T}"/>'s value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The location, or <see langword="null"/> for none.</returns>
    public string? LocationFor(object? value) =>
        value is null || ValueType.IsInstanceOfType(value)
            ? location(value)
            : throw new InvalidOperationException(
                $"WithCreatedLocation<{ValueType.Name}> can't make a location from a "
                + $"{value.GetType().Name}: the endpoint returns a different value.");
}
