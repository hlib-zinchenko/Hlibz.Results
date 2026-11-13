using System.Diagnostics.CodeAnalysis;
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

        return EnvelopeTypeOf(resultType) is { } envelopeType
            ? new ProducesResponseTypeMetadata(StatusCodes.Status200OK, envelopeType, [JsonContentType])
            : new ProducesResponseTypeMetadata(StatusCodes.Status200OK);
    }

    /// <summary>
    /// The envelope type documented for <paramref name="resultType"/>, or <see langword="null"/>
    /// when Native AOT has no <c>ResultEnvelope&lt;T&gt;</c> for its <c>T</c> (the value type of a
    /// result the app never sends as an envelope), so the response goes undocumented.
    /// </summary>
    [UnconditionalSuppressMessage(
        "AOT",
        "IL3050:Calling members annotated with 'RequiresDynamicCodeAttribute' may break functionality when AOT compiling.",
        Justification = "Documentation only; a missing instantiation is caught and left undocumented.")]
    private static Type? EnvelopeTypeOf(Type resultType)
    {
        if (!resultType.IsGenericType)
        {
            return typeof(ResultEnvelope);
        }

        try
        {
            return typeof(ResultEnvelope<>).MakeGenericType(resultType.GetGenericArguments());
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// The converter for a handler returning <paramref name="returnType"/>. A
    /// <see cref="Result{T}"/> reaches its typed conversion through
    /// <see cref="Result.Accept{TOut}"/>, which Native AOT compiles ahead of time for every
    /// <c>T</c> the app returns, rather than through a generic method made at runtime. A handler
    /// declared to return a plain <see cref="Result"/> is sent as one, whatever it returns.
    /// </summary>
    private static Func<Result, IResult>? ConverterFor(Type returnType, bool envelope) =>
        ResultTypeOf(returnType) switch
        {
            null => null,
            { IsGenericType: true } when envelope => static result => result.Accept(EnvelopeVisitor.Instance),
            { IsGenericType: true } => static result => result.Accept(ResponseVisitor.Instance),
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

    /// <summary>Sends a result as <c>ToHttpResult()</c> does, typed.</summary>
    private sealed class ResponseVisitor : IResultVisitor<IResult>
    {
        public static readonly ResponseVisitor Instance = new();

        public IResult Visit(Result result) => result.ToHttpResult();

        public IResult Visit<T>(Result<T> result) => result.ToHttpResult();
    }

    /// <summary>Sends a result as <c>ToEnvelopeHttpResult()</c> does, typed.</summary>
    private sealed class EnvelopeVisitor : IResultVisitor<IResult>
    {
        public static readonly EnvelopeVisitor Instance = new();

        public IResult Visit(Result result) => result.ToEnvelopeHttpResult();

        public IResult Visit<T>(Result<T> result) => result.ToEnvelopeHttpResult();
    }
}
