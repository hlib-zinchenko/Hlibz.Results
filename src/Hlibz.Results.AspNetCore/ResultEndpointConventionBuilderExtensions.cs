using System.Runtime.CompilerServices;

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
    /// response as it is sent: the success status code (<c>200</c> unless
    /// <see cref="WithSuccessStatusCode{TBuilder}"/>, <see cref="WithCreatedLocation{T}"/> or
    /// <see cref="ResultHttpOptions.SuccessStatusCodes"/> says otherwise) with a <c>T</c> (or with
    /// no body for a <see cref="Result"/> or a <c>204</c>) instead of the <c>Result&lt;T&gt;</c>
    /// ASP.NET Core would infer.
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
    /// and documents the success response as one: the success status code (as for
    /// <see cref="WithResultResponses{TBuilder}"/>) with a <see cref="ResultEnvelope{T}"/> (or a
    /// <see cref="ResultEnvelope"/>) instead of the <c>Result&lt;T&gt;</c> ASP.NET Core would
    /// infer. With <see cref="ResultHttpOptions.CreateEnvelope"/> set, it is documented without a
    /// type, since the shape is the API's own. An endpoint whose success status code is
    /// <c>204</c> fails when it is built, since an envelope always has a body.
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

    /// <summary>
    /// Sends a successful result from the endpoint, or from every endpoint in the route group,
    /// with <paramref name="statusCode"/>, in place of
    /// <see cref="ResultHttpOptions.SuccessStatusCodes"/> and
    /// <see cref="ResultHttpOptions.StatusCodes"/>. Applies to <see cref="WithResultResponses{TBuilder}"/>,
    /// <see cref="WithResultEnvelopes{TBuilder}"/> (which document it) and to direct
    /// <c>ToHttpResult()</c> calls alike. The most specific one wins: an endpoint's over its
    /// group's, an inner group's over an outer one's.
    /// </summary>
    /// <remarks>
    /// A <c>204</c> is sent without a body, so a <see cref="Result{T}"/>'s value is dropped, and it
    /// can't be used with envelopes, which always have one.
    /// </remarks>
    /// <example>
    /// <code>
    /// RouteGroupBuilder orders = app.MapGroup("/api/orders").WithResultResponses();
    ///
    /// orders.MapPost("", (OrderService service) =&gt; service.Create())
    ///     .WithSuccessStatusCode(StatusCodes.Status201Created);
    /// </code>
    /// </example>
    /// <typeparam name="TBuilder">The builder's type.</typeparam>
    /// <param name="builder">An endpoint, a route group, or any other convention builder.</param>
    /// <param name="statusCode">A <c>2xx</c> status code.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="statusCode"/> isn't
    /// <c>2xx</c>.</exception>
    public static TBuilder WithSuccessStatusCode<TBuilder>(this TBuilder builder, int statusCode)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.WithMetadata(new SuccessStatusCodeAttribute(statusCode));
    }

    /// <summary>
    /// Sends a successful <see cref="Result{T}"/> from the endpoint as <c>201 Created</c> with a
    /// <c>Location</c> made from its value, whatever success status code it would have had.
    /// Applies to <see cref="WithResultResponses{TBuilder}"/>,
    /// <see cref="WithResultEnvelopes{TBuilder}"/> (which document the <c>201</c>) and to direct
    /// <c>ToHttpResult()</c> calls alike; for a single call,
    /// <see cref="HttpResultExtensions.ToCreatedHttpResult{T}"/> does the same.
    /// </summary>
    /// <remarks>
    /// With <see cref="WithResultResponses{TBuilder}"/> or <see cref="WithResultEnvelopes{TBuilder}"/>,
    /// a handler that doesn't return a <see cref="Result{T}"/> of a <typeparamref name="T"/>
    /// fails when the endpoint is built.
    /// </remarks>
    /// <example>
    /// <code>
    /// orders.MapPost("", (OrderService service) =&gt; service.Create()) // Result&lt;OrderDto&gt;
    ///     .WithCreatedLocation&lt;OrderDto&gt;(order =&gt; $"/api/orders/{order.Id}");
    /// </code>
    /// </example>
    /// <typeparam name="T">The type of the result's value.</typeparam>
    /// <param name="builder">The endpoint.</param>
    /// <param name="location">Makes the <c>Location</c> from the value; <see langword="null"/>
    /// leaves the header out.</param>
    /// <returns>The same builder.</returns>
    public static RouteHandlerBuilder WithCreatedLocation<T>(
        this RouteHandlerBuilder builder,
        Func<T, string?> location)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(location);
        return builder.WithMetadata(
            new CreatedLocationMetadata(typeof(T), value => location((T)value!)));
    }

    /// <summary>
    /// Documents the errors of <typeparamref name="TErrorSet"/> on an endpoint whose handler
    /// doesn't return the <see cref="Result{T, TError}"/> that would declare them: one that returns
    /// an <see cref="IResult"/> (a file, or a result it sends itself with
    /// <see cref="HttpResultExtensions.ToHttpResult{T}"/>), or whose result a filter wraps. As
    /// <see cref="WithResultResponses{TBuilder}"/> documents a declared error set: a response per
    /// status code the errors are sent with (unless the endpoint documents that status code
    /// itself) and every error in <see cref="DeclaredErrorsMetadata"/>, for documentation that
    /// shows each error and its data (as Hlibz.OpenApi.Results does).
    /// </summary>
    /// <remarks>
    /// Documentation only: how the endpoint sends its errors is up to its handler. They are
    /// documented as <see cref="ResultEnvelope"/>s when <see cref="ResultHttpOptions.UseEnvelope"/>
    /// is on (with no type for an envelope of the API's own,
    /// <see cref="ResultHttpOptions.CreateEnvelope"/>), as <see cref="ErrorResponse"/>s otherwise.
    /// Call it once per error set; with a handler that returns a <c>Result&lt;T, TError&gt;</c>
    /// too, both sets are documented.
    /// </remarks>
    /// <example>
    /// <code>
    /// reports.MapGet("{id}/export", ExportReport) // returns IResult: a file, or result.ToHttpResult()
    ///     .ProducesErrorSet&lt;ExportReportError&gt;();
    /// </code>
    /// </example>
    /// <typeparam name="TErrorSet">The error set: a <see langword="union"/> of error types.</typeparam>
    /// <param name="builder">The endpoint.</param>
    /// <returns>The same builder.</returns>
    /// <exception cref="InvalidOperationException"><typeparamref name="TErrorSet"/> isn't an error
    /// set (see <see cref="ErrorSetDescriptor.For"/>).</exception>
    public static RouteHandlerBuilder ProducesErrorSet<TErrorSet>(this RouteHandlerBuilder builder)
        where TErrorSet : struct, IUnion
    {
        ArgumentNullException.ThrowIfNull(builder);
        ResultEndpoints.DocumentErrorSet(builder, typeof(TErrorSet));
        return builder;
    }
}
