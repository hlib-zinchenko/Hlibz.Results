namespace Hlibz.Results.AspNetCore;

/// <summary>
/// Endpoint metadata: the errors the handler declares by returning a
/// <see cref="Result{T, TError}"/>, each with how it is sent. Added by
/// <see cref="ResultEndpointConventionBuilderExtensions.WithResultResponses{TBuilder}"/> and
/// <see cref="ResultEndpointConventionBuilderExtensions.WithResultEnvelopes{TBuilder}"/> alongside
/// a response per status code, for documentation that shows each error and its data (as
/// Hlibz.OpenApi.Results does).
/// </summary>
public sealed class DeclaredErrorsMetadata
{
    /// <summary>
    /// Gets the declared errors, in the error set's order.
    /// </summary>
    public required IReadOnlyList<DeclaredErrorResponse> Errors { get; init; }
}

/// <summary>
/// A declared error and the response it is sent as.
/// </summary>
#pragma warning disable SA1402 // One metadata shape, read together.
public sealed class DeclaredErrorResponse
#pragma warning restore SA1402
{
    /// <summary>
    /// Gets the error.
    /// </summary>
    public required ErrorDescriptor Error { get; init; }

    /// <summary>
    /// Gets the HTTP status code it is sent with (<c>200</c> under
    /// <see cref="ResultHttpOptions.EnvelopeAlwaysOk"/>).
    /// </summary>
    public required int StatusCode { get; init; }

    /// <summary>
    /// Gets the body's type: <see cref="ErrorResponse"/>, <see cref="ResultEnvelope"/>, or
    /// <see langword="null"/> for an envelope of the app's own
    /// (<see cref="ResultHttpOptions.CreateEnvelope"/>).
    /// </summary>
    public Type? ResponseType { get; init; }
}
