using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;

namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// The filter and the documentation behind
/// <see cref="ResultEndpointConventionBuilderExtensions.WithResultResponses{TBuilder}"/> and
/// <see cref="ResultEndpointConventionBuilderExtensions.WithResultEnvelopes{TBuilder}"/>.
/// </summary>
internal static class ResultEndpoints
{
    private const string JsonContentType = "application/json";

    private static readonly MethodInfo ResponseConverter = typeof(ResultEndpoints)
        .GetMethod(nameof(ToResponse), BindingFlags.Public | BindingFlags.Static)!;

    private static readonly MethodInfo EnvelopeConverter = typeof(ResultEndpoints)
        .GetMethod(nameof(ToEnvelope), BindingFlags.Public | BindingFlags.Static)!;

    /// <summary>
    /// Sends a <see cref="Result{T}"/> as <see cref="HttpResultExtensions.ToHttpResult{T}"/>
    /// does. Public only so the converter can be bound without bypassing accessibility.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result, a <see cref="Result{T}"/>.</param>
    /// <returns>The response.</returns>
    public static IResult ToResponse<T>(Result result) => ((Result<T>)result).ToHttpResult();

    /// <summary>
    /// Sends a <see cref="Result{T}"/> as its envelope. Public only so the converter can be bound
    /// without bypassing accessibility.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="result">The result, a <see cref="Result{T}"/>.</param>
    /// <returns>The response.</returns>
    public static IResult ToEnvelope<T>(Result result) =>
        ((Result<T>)result).ToEnvelopeHttpResult();

    /// <summary>
    /// Adds the filter and the documentation to every endpoint <paramref name="builder"/>
    /// builds.
    /// </summary>
    /// <param name="builder">An endpoint, a route group, or any other convention builder.</param>
    /// <param name="envelope">Whether results are sent as envelopes.</param>
    public static void Apply(IEndpointConventionBuilder builder, bool envelope)
    {
        builder.AddEndpointFilterFactory((context, next) => CreateFilter(context, next, envelope));
        builder.Finally(endpoint => Document(endpoint, envelope));
    }

    /// <summary>
    /// The endpoint's filter: the handler's result turned into its response, or no filter at all
    /// when the handler doesn't return a result.
    /// </summary>
    private static EndpointFilterDelegate CreateFilter(
        EndpointFilterFactoryContext context,
        EndpointFilterDelegate next,
        bool envelope)
    {
        if (ConverterFor(context.MethodInfo.ReturnType, envelope) is not { } convert)
        {
            return next;
        }

        // Anything else comes from a filter that short-circuited the handler: send it as it is.
        return async invocation =>
        {
            object? value = await next(invocation).ConfigureAwait(false);
            return value is Result result ? convert(result) : value;
        };
    }

    /// <summary>
    /// Replaces the <c>Result</c> ASP.NET Core documents for the handler's return type with what
    /// is actually sent on success.
    /// </summary>
    private static void Document(EndpointBuilder endpoint, bool envelope)
    {
        IList<object> metadata = endpoint.Metadata;
        if (metadata.OfType<MethodInfo>().FirstOrDefault() is not { } handler
            || ResultTypeOf(handler.ReturnType) is not { } resultType)
        {
            return;
        }

        for (int index = metadata.Count - 1; index >= 0; index--)
        {
            if (metadata[index] is IProducesResponseTypeMetadata { Type: { } type }
                && typeof(Result).IsAssignableFrom(type))
            {
                metadata.RemoveAt(index);
            }
        }

        metadata.Add(envelope ? EnvelopeResponse(endpoint, resultType) : PlainResponse(resultType));
    }

    private static ProducesResponseTypeMetadata PlainResponse(Type resultType) =>
        resultType.IsGenericType
            ? new ProducesResponseTypeMetadata(
                StatusCodes.Status200OK,
                resultType.GetGenericArguments()[0],
                [JsonContentType])
            : new ProducesResponseTypeMetadata(StatusCodes.Status200OK);

    private static ProducesResponseTypeMetadata EnvelopeResponse(
        EndpointBuilder endpoint,
        Type resultType)
    {
        if (endpoint.ApplicationServices is { } services
            && ResultResponses.GetOptions(services).CreateEnvelope is not null)
        {
            return new ProducesResponseTypeMetadata(StatusCodes.Status200OK);
        }

        Type envelopeType = resultType.IsGenericType
            ? typeof(ResultEnvelope<>).MakeGenericType(resultType.GetGenericArguments())
            : typeof(ResultEnvelope);
        return new ProducesResponseTypeMetadata(
            StatusCodes.Status200OK,
            envelopeType,
            [JsonContentType]);
    }

    private static Func<Result, IResult>? ConverterFor(Type returnType, bool envelope) =>
        ResultTypeOf(returnType) switch
        {
            null => null,
            { IsGenericType: true } typed => (envelope ? EnvelopeConverter : ResponseConverter)
                .MakeGenericMethod(typed.GetGenericArguments()[0])
                .CreateDelegate<Func<Result, IResult>>(),
            _ when envelope => static result => result.ToEnvelopeHttpResult(),
            _ => static result => result.ToHttpResult(),
        };

    /// <summary>
    /// <see cref="Result{T}"/> or <see cref="Result"/> when <paramref name="returnType"/> is one
    /// (or derives from one), optionally in a <c>Task</c> or <c>ValueTask</c>; otherwise
    /// <see langword="null"/>.
    /// </summary>
    private static Type? ResultTypeOf(Type returnType)
    {
        Type type = returnType.IsGenericType
                    && (returnType.GetGenericTypeDefinition() == typeof(Task<>)
                        || returnType.GetGenericTypeDefinition() == typeof(ValueTask<>))
            ? returnType.GetGenericArguments()[0]
            : returnType;

        for (Type? current = type; current is not null; current = current.BaseType)
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(Result<>))
            {
                return current;
            }
        }

        return typeof(Result).IsAssignableFrom(type) ? typeof(Result) : null;
    }
}
