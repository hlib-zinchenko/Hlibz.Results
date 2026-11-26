using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;

namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// Turns a catalog into what <see cref="ErrorCodeEndpointRouteBuilderExtensions.MapErrorCodes"/>
/// and the build-time files publish.
/// </summary>
internal static class ErrorCodeInfos
{
    private static readonly JsonSchemaExporterOptions SchemaOptions = new()
    {
        TreatNullObliviousAsNonNullable = true,
    };

    /// <summary>
    /// The catalog's errors, with the status codes <paramref name="options"/> send them with and
    /// their details' schemas as <paramref name="json"/> writes them.
    /// </summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="options">The app's result options.</param>
    /// <param name="json">The app's JSON options.</param>
    /// <returns>The entries, ordered by code.</returns>
    public static IReadOnlyList<ErrorCodeInfo> For(
        IErrorCodeCatalog catalog,
        ResultHttpOptions options,
        JsonSerializerOptions json) =>
    [
        .. catalog.All.Select(error => new ErrorCodeInfo
        {
            Code = error.Code,
            Status = ResultResponses.StatusCodeFor(error.Status, options),
            Message = error.Message,
            Details = DetailsSchema(error, json),
        }),
    ];

    private static JsonNode? DetailsSchema(ErrorDescriptor error, JsonSerializerOptions json)
    {
        if (!error.HasDetails)
        {
            return null;
        }

        // A ValidationFailed goes out as validationErrors, not details.
        if (error.Type == typeof(ValidationFailed))
        {
            return null;
        }

        // The schema of what is written: numbers go out as numbers even when the app reads them
        // from strings too (the web defaults).
        JsonSerializerOptions written = new(json) { NumberHandling = JsonNumberHandling.Strict };
        try
        {
            return written.GetTypeInfo(error.Type).GetJsonSchemaAsNode(SchemaOptions);
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
