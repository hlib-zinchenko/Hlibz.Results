using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;

namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// The filter and the documentation behind
/// <see cref="ResultEndpointConventionBuilderExtensions.WithResultEnvelopes{TBuilder}"/>.
/// </summary>
internal static class ResultEnvelopeEndpoints
{
    private const string JsonContentType = "application/json";

    private static readonly MethodInfo TypedConverter = typeof(ResultEnvelopeEndpoints)
        .GetMethod(nameof(ToEnvelope), BindingFlags.Public | BindingFlags.Static)!;

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
    /// The endpoint's filter: the handler's result turned into its envelope, or no filter at all
    /// when the handler doesn't return a result.
    /// </summary>
    /// <param name="context">The endpoint's handler.</param>
    /// <param name="next">The rest of the filter pipeline.</param>
    /// <returns>The filter, or <paramref name="next"/>.</returns>
    public static EndpointFilterDelegate CreateFilter(
        EndpointFilterFactoryContext context,
        EndpointFilterDelegate next)
    {
        if (ConverterFor(context.MethodInfo.ReturnType) is not { } convert)
        {
            return next;
        }

        return async invocation => await next(invocation).ConfigureAwait(false) is Result result
            ? convert(result)
            : null;
    }

    /// <summary>
    /// Replaces the <c>Result</c> ASP.NET Core documents for the handler's return type with its
    /// envelope.
    /// </summary>
    /// <param name="endpoint">The endpoint, its metadata final.</param>
    public static void Document(EndpointBuilder endpoint)
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

        if (endpoint.ApplicationServices is { } services
            && ResultResponses.GetOptions(services).CreateEnvelope is not null)
        {
            metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status200OK));
            return;
        }

        Type envelopeType = resultType.IsGenericType
            ? typeof(ResultEnvelope<>).MakeGenericType(resultType.GetGenericArguments())
            : typeof(ResultEnvelope);
        metadata.Add(new ProducesResponseTypeMetadata(
            StatusCodes.Status200OK,
            envelopeType,
            [JsonContentType]));
    }

    private static Func<Result, IResult>? ConverterFor(Type returnType) =>
        ResultTypeOf(returnType) switch
        {
            null => null,
            { IsGenericType: true } typed => TypedConverter
                .MakeGenericMethod(typed.GetGenericArguments()[0])
                .CreateDelegate<Func<Result, IResult>>(),
            _ => static result => result.ToEnvelopeHttpResult(),
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
