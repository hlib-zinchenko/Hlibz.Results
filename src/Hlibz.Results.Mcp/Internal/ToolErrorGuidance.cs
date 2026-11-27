using System.Text.RegularExpressions;

namespace Hlibz.Results.Mcp.Internal;

/// <summary>Fills a guidance template's <c>{name}</c> placeholders from an error's details.</summary>
internal static partial class ToolErrorGuidance
{
    // The placeholders template names, in order.
    public static IEnumerable<string> PlaceholdersOf(string template) =>
        Placeholder().Matches(template).Select(match => match.Groups[1].Value);

    // template with each placeholder replaced by its detail; one the details don't have stays as it is.
    public static string Fill(string template, JsonObject? details) =>
        Placeholder().Replace(
            template,
            match => details?[match.Groups[1].Value] is { } value ? Render(value) : match.Value);

    // An object (validation errors) as "key: value; key: value", an array as its items.
    private static string Render(JsonNode value) => value switch
    {
        JsonObject entries => string.Join(
            "; ",
            entries.Select(entry => $"{entry.Key}: {(entry.Value is null ? string.Empty : Render(entry.Value))}")),
        JsonArray items => string.Join(" ", items.Select(item => item is null ? string.Empty : Render(item))),
        JsonValue text when text.TryGetValue(out string? s) => s,
        _ => value.ToJsonString(),
    };

    [GeneratedRegex(@"\{(\w+)\}", RegexOptions.CultureInvariant)]
    private static partial Regex Placeholder();
}
