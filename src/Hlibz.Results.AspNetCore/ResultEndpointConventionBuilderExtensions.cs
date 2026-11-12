using Hlibz.Results.AspNetCore.Internal;

using Microsoft.AspNetCore.Builder;

namespace Hlibz.Results.AspNetCore;

/// <summary>
/// Lets minimal API handlers return a <see cref="Result"/> or <see cref="Result{T}"/> as it is.
/// </summary>
public static class ResultEndpointConventionBuilderExtensions
{
    /// <summary>
    /// Sends the <see cref="Result"/> or <see cref="Result{T}"/> an endpoint's handler returns
    /// (optionally in a <see cref="Task{TResult}"/> or <see cref="ValueTask{TResult}"/>) exactly
    /// as <see cref="HttpResultExtensions.ToHttpResult{T}"/> sends it, and documents the success
    /// response as it is sent: <c>200</c> with a <c>T</c> (or with no body for a
    /// <see cref="Result"/>) instead of the <c>Result&lt;T&gt;</c> ASP.NET Core would infer.
    /// </summary>
    /// <remarks>
    /// Whether the handler returns a result is decided once per endpoint, at startup: any other
    /// endpoint is left alone, so this can be applied to a route group or to every endpoint. A
    /// response a filter sends in place of the handler's is sent as it is. The failures it can
    /// send aren't documented: declare them on the endpoint. Use this or
    /// <see cref="WithResultEnvelopes{TBuilder}"/> on an endpoint, not both.
    /// </remarks>
    /// <example>
    /// <code>
    /// RouteGroupBuilder orders = app.MapGroup("/api/orders").WithResultResponses();
    ///
    /// orders.MapGet("{id}", (int id, OrderService service) => service.Find(id)); // Result&lt;OrderDto&gt;
    /// </code>
    /// </example>
    /// <typeparam name="TBuilder">The builder's type.</typeparam>
    /// <param name="builder">An endpoint, a route group, or any other convention builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder WithResultResponses<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ResultEndpoints.Apply(builder, envelope: false);
        return builder;
    }

    /// <summary>
    /// Sends the <see cref="Result"/> or <see cref="Result{T}"/> an endpoint's handler returns
    /// (optionally in a <see cref="Task{TResult}"/> or <see cref="ValueTask{TResult}"/>) as an
    /// envelope, exactly as <see cref="HttpResultExtensions.ToEnvelopeHttpResult{T}"/> sends it,
    /// and documents the success response as one: <c>200</c> with a
    /// <see cref="ResultEnvelope{T}"/> (or a <see cref="ResultEnvelope"/>) instead of the
    /// <c>Result&lt;T&gt;</c> ASP.NET Core would infer. With
    /// <see cref="ResultHttpOptions.CreateEnvelope"/> set, the <c>200</c> is documented without a
    /// type, since the shape is the API's own.
    /// </summary>
    /// <remarks>
    /// Whether the handler returns a result is decided once per endpoint, at startup: any other
    /// endpoint is left alone, so this can be applied to a route group or to every endpoint. A
    /// response a filter sends in place of the handler's is sent as it is. The failures it can
    /// send aren't documented: declare them on the endpoint. Use this or
    /// <see cref="WithResultResponses{TBuilder}"/> on an endpoint, not both.
    /// </remarks>
    /// <example>
    /// <code>
    /// RouteGroupBuilder orders = app.MapGroup("/api/orders").WithResultEnvelopes();
    ///
    /// orders.MapGet("{id}", (int id, OrderService service) => service.Find(id)); // Result&lt;OrderDto&gt;
    /// </code>
    /// </example>
    /// <typeparam name="TBuilder">The builder's type.</typeparam>
    /// <param name="builder">An endpoint, a route group, or any other convention builder.</param>
    /// <returns>The same builder.</returns>
    public static TBuilder WithResultEnvelopes<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ResultEndpoints.Apply(builder, envelope: true);
        return builder;
    }
}
