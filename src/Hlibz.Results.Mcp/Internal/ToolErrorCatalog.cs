using Microsoft.Extensions.Options;

namespace Hlibz.Results.Mcp.Internal;

/// <summary>
/// Which errors each tool can return (its <see cref="ToolErrorsAttribute"/>s, then the
/// <see cref="IToolErrorSource"/>s), written into its <c>_meta</c> and checked against the guidance.
/// </summary>
/// <param name="errorOptions">The guidance.</param>
/// <param name="sources">The app's error sources.</param>
internal sealed class ToolErrorCatalog(IOptions<ToolErrorOptions> errorOptions, IEnumerable<IToolErrorSource> sources)
    : IPostConfigureOptions<McpServerOptions>, IValidateOptions<McpServerOptions>
{
    // Gets the errors tool can return, each once, declared ones first.
    public IReadOnlyList<Type> ErrorsOf(McpServerTool tool) =>
    [
        .. tool.Metadata.OfType<ToolErrorsAttribute>().SelectMany(declared => declared.ErrorTypes)
            .Concat(sources.SelectMany(source => source.ErrorsOf(tool)))
            .Distinct(),
    ];

    // Lists each tool's errors in its _meta, replacing what was listed there.
    public void PostConfigure(string? name, McpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        foreach (McpServerTool tool in options.ToolCollection ?? [])
        {
            IReadOnlyList<Type> errors = ErrorsOf(tool);
            if (errors.Count == 0)
            {
                continue;
            }

            JsonArray entries = [];
            foreach (Type error in errors.Where(IsError))
            {
                entries.Add((JsonNode)Entry(error));
            }

            tool.ProtocolTool.Meta ??= [];
            tool.ProtocolTool.Meta[ToolErrorOptions.MetaKey] = entries;
        }
    }

    // Fails when a tool's error isn't an error type or has no guidance (with
    // ToolErrorOptions.RequireGuidance), or guidance names a detail its error doesn't have.
    public ValidateOptionsResult Validate(string? name, McpServerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ToolErrorOptions errors = errorOptions.Value;
        List<string> problems = [];
        foreach (McpServerTool tool in options.ToolCollection ?? [])
        {
            foreach (Type error in ErrorsOf(tool))
            {
                if (!IsError(error))
                {
                    problems.Add($"Tool '{tool.ProtocolTool.Name}' lists {error.FullName}, which isn't an error type.");
                }
                else if (errors.RequireGuidance && !errors.Guidance.ContainsKey(error))
                {
                    problems.Add(
                        $"Tool '{tool.ProtocolTool.Name}' can fail with {error.Name} ({ErrorDescriptor.For(error).Code}), "
                        + $"which has no guidance: add errors.Guide<{error.Name}>(\"...\") to WithToolErrors.");
                }
            }
        }

        foreach ((Type error, string template) in errors.Guidance)
        {
            HashSet<string> details = [.. ToolErrorDetails.NamesOf(error, errors.SerializerOptions)];
            problems.AddRange(ToolErrorGuidance.PlaceholdersOf(template)
                .Where(placeholder => !details.Contains(placeholder))
                .Select(placeholder =>
                    $"{error.Name}'s guidance names {{{placeholder}}}, which isn't one of its details"
                    + (details.Count == 0 ? " (it has none)." : $" ({string.Join(", ", details.Select(detail => $"{{{detail}}}"))}).")));
        }

        return problems.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(problems.Distinct());
    }

    private static bool IsError(Type type) => type.IsSubclassOf(typeof(Error)) && !type.IsAbstract;

    private JsonObject Entry(Type error)
    {
        ErrorDescriptor descriptor = ErrorDescriptor.For(error);
        JsonObject entry = new() { ["code"] = descriptor.Code, ["description"] = descriptor.Message };
        if (errorOptions.Value.Guidance.TryGetValue(error, out string? guidance))
        {
            entry["guidance"] = guidance;
        }

        return entry;
    }
}
