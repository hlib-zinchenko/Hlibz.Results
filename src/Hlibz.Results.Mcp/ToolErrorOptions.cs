namespace Hlibz.Results.Mcp;

/// <summary>
/// How tool errors go out: the guidance for each error, and the JSON their details are written
/// with. Set through <see cref="McpServerBuilderToolErrorExtensions.WithToolErrors"/>.
/// </summary>
/// <remarks>
/// An error's <see cref="Error.Message"/> says what failed, for logs; its guidance says what to do
/// next, in terms of the server's tools, for the model. <c>{name}</c> placeholders are the error's
/// details as they go out (<see cref="SerializerOptions"/>' names: <c>{todoListId}</c>); a
/// <see cref="ValidationFailed"/>'s one detail is <c>{errors}</c>, written as
/// <c>key: message; key: message</c>.
/// </remarks>
public sealed class ToolErrorOptions
{
    /// <summary>
    /// The <c>_meta</c> key each tool lists its errors under: an array of
    /// <c>{ "code", "description", "guidance"? }</c>, the description being the error's
    /// <see cref="ErrorAttribute">declared</see> message. Hlibz.McpDocs.AspNetCore reads the same key.
    /// </summary>
    public const string MetaKey = "hlibz/errors";

    private readonly Dictionary<Type, string> _guidance = [];

    /// <summary>Gets the guidance templates, by error type.</summary>
    public IReadOnlyDictionary<Type, string> Guidance => _guidance;

    /// <summary>
    /// Gets or sets a value indicating whether every error a tool declares needs guidance: when
    /// set (the default), the server doesn't start while one has none. Without guidance an error
    /// goes out with its message.
    /// </summary>
    public bool RequireGuidance { get; set; } = true;

    /// <summary>
    /// Gets or sets the JSON options errors' details are written with: by default the MCP SDK's
    /// (<see cref="McpJsonUtilities.DefaultOptions"/>, camelCase), as tools' own results. Under
    /// Native AOT, an error type these options can't write goes out without details. A
    /// <see cref="ValidationFailed"/>'s keys are converted with its naming policy, segment by
    /// segment (<c>items[0].unitPrice</c>).
    /// </summary>
    public JsonSerializerOptions SerializerOptions { get; set; } = McpJsonUtilities.DefaultOptions;

    /// <summary>Sets the guidance for <typeparamref name="TError"/>, replacing any it had.</summary>
    /// <typeparam name="TError">The error.</typeparam>
    /// <param name="guidance">What a model should do about it, with the error's details as
    /// <c>{name}</c> placeholders.</param>
    /// <returns>These options, for chaining.</returns>
    public ToolErrorOptions Guide<TError>(string guidance)
        where TError : Error
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guidance);
        _guidance[typeof(TError)] = guidance;
        return this;
    }
}
