namespace Hlibz.Results.Mcp.Internal;

/// <summary>An error as a tool result: guidance for the model, code and details for programs.</summary>
internal static class ToolErrorResults
{
    // isError, "{code}: {guidance}" as text (the error's message when it has no guidance) and { code,
    // details? } as structured content.
    public static CallToolResult Create(Error error, ToolErrorOptions options)
    {
        JsonObject? details = ToolErrorDetails.Of(error, options.SerializerOptions);
        string text = options.Guidance.TryGetValue(error.GetType(), out string? template)
            ? ToolErrorGuidance.Fill(template, details)
            : error.Message;
        JsonObject structured = new() { ["code"] = error.Code };
        if (details is not null)
        {
            structured["details"] = details;
        }

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = $"{error.Code}: {text}" }],
            StructuredContent = JsonElement.Parse(structured.ToJsonString()),
        };
    }
}
