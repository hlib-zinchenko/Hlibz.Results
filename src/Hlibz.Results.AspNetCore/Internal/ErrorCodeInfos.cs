using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

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
        TransformSchemaNode = DescribeIds,
    };

    /// <summary>The <see cref="SingleValueStruct"/> answer for each struct, looked up once.</summary>
    private static readonly ConcurrentDictionary<Type, Type?> SingleValueStructs = new();

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

    /// <summary>
    /// Describes a strongly-typed ID as the value it wraps: the exporter can't see through a
    /// custom <see cref="JsonConverter"/>, so a <c>CityId</c> over a <see cref="Guid"/> comes out
    /// as <see langword="true"/> (any value) rather than <c>{ "type": "string", "format": "uuid" }</c>,
    /// and a list of them as an array without <c>items</c>; an enum written by a
    /// <see cref="JsonStringEnumConverter"/> comes out as its <c>enum</c> alone, given its
    /// <c>type</c> here. An ID is a struct whose only public
    /// instance property is <c>Value</c>, the default rule of Hlibz.OpenApi's
    /// <c>AddStronglyTypedIds()</c>.
    /// </summary>
    /// <param name="context">The type the schema stands for.</param>
    /// <param name="schema">The exporter's schema.</param>
    /// <returns>The schema, with any ID the exporter couldn't describe described.</returns>
    private static JsonNode DescribeIds(JsonSchemaExporterContext context, JsonNode schema)
    {
        JsonTypeInfo typeInfo = context.TypeInfo;
        if (schema.GetValueKind() == JsonValueKind.True)
        {
            return IdSchema(typeInfo.Type, typeInfo.Options) ?? schema;
        }

        if (schema is JsonObject enumSchema && (Nullable.GetUnderlyingType(typeInfo.Type) ?? typeInfo.Type).IsEnum)
        {
            AddEnumType(enumSchema);
            return schema;
        }

        if (schema is JsonObject array
            && typeInfo is { Kind: JsonTypeInfoKind.Enumerable, ElementType: { } element }
            && !array.ContainsKey("items")
            && IdSchema(element, typeInfo.Options) is { } items)
        {
            array["items"] = items;
        }

        return schema;
    }

    /// <summary>
    /// Gives an enum's schema the <c>type</c> of its values (<c>string</c> for names,
    /// <c>integer</c> for numbers, with <c>null</c> for a nullable one) when it has none.
    /// </summary>
    /// <param name="schema">The enum's schema.</param>
    private static void AddEnumType(JsonObject schema)
    {
        if (schema.ContainsKey("type") || schema["enum"] is not JsonArray values)
        {
            return;
        }

        JsonValueKind[] kinds = [.. values.Select(value => value?.GetValueKind() ?? JsonValueKind.Null).Distinct()];
        JsonValueKind[] named = [.. kinds.Where(kind => kind != JsonValueKind.Null)];
        string? type = named switch
        {
            [JsonValueKind.String] => "string",
            [JsonValueKind.Number] => "integer",
            _ => null,
        };
        if (type is null)
        {
            return;
        }

        schema["type"] = kinds.Contains(JsonValueKind.Null)
            ? new JsonArray(type, "null")
            : type;
    }

    /// <summary>The schema of the value a (nullable) ID wraps.</summary>
    /// <param name="type">The type to describe.</param>
    /// <param name="options">The JSON options the schema is for.</param>
    /// <returns>The schema, or <see langword="null"/> for a type that isn't an ID.</returns>
    private static JsonNode? IdSchema(Type type, JsonSerializerOptions options)
    {
        Type? nullableOf = Nullable.GetUnderlyingType(type);
        if (SingleValueStruct(nullableOf ?? type) is not { } valueType)
        {
            return null;
        }

        JsonNode value;
        try
        {
            value = options.GetTypeInfo(valueType).GetJsonSchemaAsNode(SchemaOptions);
        }
        catch (NotSupportedException)
        {
            // The app's resolver doesn't know the value type (a source-generated context without it).
            return null;
        }

        if (nullableOf is not null && value is JsonObject { } valueObject)
        {
            AllowNull(valueObject);
        }

        return value;
    }

    private static void AllowNull(JsonObject schema)
    {
        switch (schema["type"])
        {
            case JsonValue type when type.GetValue<string>() is var name && name != "null":
                schema["type"] = new JsonArray(name, "null");
                break;
            case JsonArray types when !types.Any(type => type?.GetValue<string>() == "null"):
                types.Add((JsonNode)"null");
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// A struct (not a primitive or an enum) whose only public instance property is <c>Value</c>.
    /// </summary>
    /// <param name="type">The type to check.</param>
    /// <returns>The type of <c>Value</c>, or <see langword="null"/>.</returns>
    private static Type? SingleValueStruct(Type type) =>
        type.IsValueType && !type.IsPrimitive && !type.IsEnum
            ? SingleValueStructs.GetOrAdd(type, static key => ReadSingleValue(key))
            : null;

    // An ID's Value is read by its JSON converter, so trimming keeps it; one that was trimmed
    // only leaves the ID described as any value, as the exporter wrote it.
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method",
        Justification = "A trimmed Value property only means the schema isn't rewritten.")]
    private static Type? ReadSingleValue(Type type)
    {
        PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        return properties is [{ Name: "Value" } property] ? property.PropertyType : null;
    }
}
