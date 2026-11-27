using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

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
    /// Documents <paramref name="errorSet"/>'s errors on every endpoint <paramref name="builder"/>
    /// builds, whatever its handler returns, sent as envelopes when
    /// <see cref="ResultHttpOptions.UseEnvelope"/> is on.
    /// </summary>
    /// <param name="builder">The endpoint.</param>
    /// <param name="errorSet">The error set.</param>
    /// <exception cref="InvalidOperationException"><paramref name="errorSet"/> isn't an error set.</exception>
    public static void DocumentErrorSet(IEndpointConventionBuilder builder, Type errorSet)
    {
        // Fails here, where the endpoint is mapped, rather than when the endpoints are built.
        _ = ErrorSetDescriptor.For(errorSet);
        builder.Finally(endpoint =>
        {
            ResultHttpOptions options = OptionsOf(endpoint);
            DocumentErrors(endpoint.Metadata, errorSet, options.UseEnvelope, options);
        });
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
    /// is actually sent on success, with the status code the endpoint sends it with, and checks
    /// the endpoint's success settings fit what the handler returns.
    /// </summary>
    private static void Document(EndpointBuilder endpoint, bool envelope)
    {
        IList<object> metadata = endpoint.Metadata;
        if (metadata.OfType<MethodInfo>().FirstOrDefault() is not { } handler
            || ResultTypeOf(handler.ReturnType) is not { } resultType)
        {
            return;
        }

        ResultHttpOptions options = OptionsOf(endpoint);
        int statusCode = SuccessStatusCodeOf(endpoint, resultType, options);
        if (envelope)
        {
            ResultResponses.RequireEnvelopeBody(statusCode);
        }

        for (int index = metadata.Count - 1; index >= 0; index--)
        {
            if (metadata[index] is IProducesResponseTypeMetadata { Type: { } type }
                && typeof(Result).IsAssignableFrom(type))
            {
                metadata.RemoveAt(index);
            }
        }

        metadata.Add(
            envelope
                ? EnvelopeResponse(statusCode, resultType, options)
                : PlainResponse(statusCode, resultType));

        if (ErrorSetOf(handler.ReturnType) is { } errorSet)
        {
            DocumentErrors(metadata, errorSet, envelope, options);
        }
    }

    private static ResultHttpOptions OptionsOf(EndpointBuilder endpoint) =>
        endpoint.ApplicationServices is { } services
            ? ResultResponses.GetOptions(services)
            : new ResultHttpOptions();

    /// <summary>
    /// Documents the errors <paramref name="errorSet"/> declares: one response per status code
    /// they are sent with (unless the endpoint documents that status itself), and every error in
    /// <see cref="DeclaredErrorsMetadata"/>.
    /// </summary>
    private static void DocumentErrors(
        IList<object> metadata,
        Type errorSet,
        bool envelope,
        ResultHttpOptions options)
    {
        Type? responseType = typeof(ErrorResponse);
        if (envelope)
        {
            responseType = options.CreateEnvelope is null ? typeof(ResultEnvelope) : null;
        }

        DeclaredErrorResponse[] errors =
        [
            .. ErrorSetDescriptor.For(errorSet).ErrorTypes.Select(type =>
            {
                ErrorDescriptor error = ErrorDescriptor.For(type);
                int statusCode = ResultResponses.StatusCodeFor(error.Status, options);
                return new DeclaredErrorResponse
                {
                    Error = error,
                    StatusCode = envelope
                        ? ResultResponses.EnvelopeHttpStatusCode(statusCode, options)
                        : statusCode,
                    ResponseType = responseType,
                };
            }),
        ];

        HashSet<int> documented =
        [
            .. metadata.OfType<IProducesResponseTypeMetadata>().Select(produces => produces.StatusCode),
        ];
        foreach (int statusCode in errors.Select(error => error.StatusCode).Where(documented.Add))
        {
            metadata.Add(responseType is null
                ? new ProducesResponseTypeMetadata(statusCode)
                : new ProducesResponseTypeMetadata(statusCode, responseType, [JsonContentType]));
        }

        metadata.Add(new DeclaredErrorsMetadata { Errors = errors });
    }

    /// <summary>
    /// The error set of a handler returning a <see cref="Result{T, TError}"/> (optionally in a
    /// <c>Task</c> or <c>ValueTask</c>), or <see langword="null"/>.
    /// </summary>
    private static Type? ErrorSetOf(Type returnType)
    {
        Type type = UnwrapTask(returnType);
        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Result<,>)
            ? type.GetGenericArguments()[1]
            : null;
    }

    private static Type UnwrapTask(Type returnType) =>
        returnType.IsGenericType
        && returnType.GetGenericTypeDefinition() is { } definition
        && (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
            ? returnType.GetGenericArguments()[0]
            : returnType;

    /// <summary>
    /// Gets whether a result of <paramref name="resultType"/> (as <see cref="ResultTypeOf"/>
    /// returns it) has a value on success: not a plain <see cref="Result"/>, nor a
    /// <c>Result&lt;Unit&gt;</c>.
    /// </summary>
    private static bool HasValue(Type resultType) =>
        resultType.IsGenericType && resultType.GetGenericArguments()[0] != typeof(Unit);

    /// <summary>
    /// The status code a success goes out with, as <see cref="ResultResponses.CreateContext"/>
    /// works it out at request time; the method's default applies only to an endpoint that
    /// answers a single method.
    /// </summary>
    private static int SuccessStatusCodeOf(
        EndpointBuilder endpoint,
        Type resultType,
        ResultHttpOptions options)
    {
        IList<object> metadata = endpoint.Metadata;
        if (metadata.OfType<CreatedLocationMetadata>().LastOrDefault() is { } created)
        {
            if (!resultType.IsGenericType
                || !created.ValueType.IsAssignableFrom(resultType.GetGenericArguments()[0]))
            {
                throw new InvalidOperationException(
                    $"WithCreatedLocation<{created.ValueType.Name}> on '{endpoint.DisplayName}' "
                    + $"needs a handler that returns a Result<{created.ValueType.Name}>.");
            }

            return StatusCodes.Status201Created;
        }

        string? method = metadata.OfType<IHttpMethodMetadata>().LastOrDefault()?.HttpMethods
            is [string single]
                ? single
                : null;
        return ResultResponses.SuccessStatusCodeFor(
            metadata.OfType<SuccessStatusCodeAttribute>().LastOrDefault(),
            method,
            options);
    }

    /// <summary>
    /// The success response without an envelope. One without a body is documented as
    /// <see langword="void"/>, as <c>.Produces(204)</c> does: ASP.NET Core's API explorer drops a
    /// response whose type is <see langword="null"/>, and then documents the handler's
    /// <c>Result</c> as the success.
    /// </summary>
    private static ProducesResponseTypeMetadata PlainResponse(int statusCode, Type resultType) =>
        HasValue(resultType) && statusCode != StatusCodes.Status204NoContent
            ? new ProducesResponseTypeMetadata(
                statusCode,
                resultType.GetGenericArguments()[0],
                [JsonContentType])
            : new ProducesResponseTypeMetadata(statusCode, typeof(void));

    private static ProducesResponseTypeMetadata EnvelopeResponse(
        int statusCode,
        Type resultType,
        ResultHttpOptions options)
    {
        // EnvelopeAlwaysOk sends every envelope as 200, whatever status it carries.
        int sent = ResultResponses.EnvelopeHttpStatusCode(statusCode, options);
        if (options.CreateEnvelope is not null)
        {
            return new ProducesResponseTypeMetadata(sent);
        }

        return EnvelopeTypeOf(resultType) is { } envelopeType
            ? new ProducesResponseTypeMetadata(sent, envelopeType, [JsonContentType])
            : new ProducesResponseTypeMetadata(sent);
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
        if (!HasValue(resultType))
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
        Type type = UnwrapTask(returnType);

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
