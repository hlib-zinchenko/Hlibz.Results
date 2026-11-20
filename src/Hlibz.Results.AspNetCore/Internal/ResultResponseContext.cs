namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// What a result's response depends on for the request being handled, made by
/// <see cref="ResultResponses.CreateContext"/>.
/// </summary>
internal sealed class ResultResponseContext
{
    /// <summary>Gets the options.</summary>
    public required ResultHttpOptions Options { get; init; }

    /// <summary>Gets the JSON settings of whatever handles the request.</summary>
    public required JsonSerializerOptions Json { get; init; }

    /// <summary>
    /// Gets the status code a success is sent with: <c>201</c> under
    /// <see cref="CreatedLocation"/>, otherwise
    /// <see cref="ResultResponses.SuccessStatusCodeFor"/>'s.
    /// </summary>
    public required int SuccessStatusCode { get; init; }

    /// <summary>Gets the endpoint's <c>WithCreatedLocation</c>, if any.</summary>
    public CreatedLocationMetadata? CreatedLocation { get; init; }

    /// <summary>
    /// Throws when the endpoint has a <c>WithCreatedLocation</c>, which needs a value, for a
    /// <see cref="Result"/> without one.
    /// </summary>
    /// <exception cref="InvalidOperationException">The endpoint has a
    /// <c>WithCreatedLocation</c>.</exception>
    public void RequireNoCreatedLocation()
    {
        if (CreatedLocation is not null)
        {
            throw new InvalidOperationException(
                "WithCreatedLocation needs a Result<T>, whose value the location is made from, "
                + "not a Result.");
        }
    }
}
