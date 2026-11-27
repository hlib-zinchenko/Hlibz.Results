using System.Text.Json.Serialization.Metadata;

namespace Hlibz.Results.Mcp.Internal;

/// <summary>An error's details as a tool error carries them, and the names a guidance template may use.</summary>
internal static class ToolErrorDetails
{
    // The one detail of a ValidationFailed: its messages, by argument.
    public const string ValidationErrors = "errors";

    // The error's data as json writes it; a ValidationFailed's messages under ValidationErrors, keyed
    // by its naming policy. null for an error without data, or one json can't write.
    public static JsonObject? Of(Error error, JsonSerializerOptions json)
    {
        if (error is ValidationFailed failed)
        {
            JsonObject errors = [];
            foreach ((string key, string[] messages) in failed.Errors)
            {
                errors[ValidationKeyFormatter.Format(key, json.PropertyNamingPolicy)] =
                    new JsonArray([.. messages.Select(message => (JsonNode?)JsonValue.Create(message))]);
            }

            return new JsonObject { [ValidationErrors] = errors };
        }

        return error.Descriptor.HasDetails
            && json.TryGetTypeInfo(error.GetType(), out JsonTypeInfo? info)
            && JsonSerializer.SerializeToNode(error, info) is JsonObject { Count: > 0 } data
                ? data
                : null;
    }

    // The names errorType's details go out with; none when json can't write it.
    public static IEnumerable<string> NamesOf(Type errorType, JsonSerializerOptions json)
    {
        if (errorType == typeof(ValidationFailed))
        {
            return [ValidationErrors];
        }

        // An ignored property ([JsonIgnore]: the base's Code, Status, Message) is listed without a getter.
        return json.TryGetTypeInfo(errorType, out JsonTypeInfo? info)
            ? info.Properties.Where(property => property.Get is not null).Select(property => property.Name)
            : [];
    }
}
