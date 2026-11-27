![Hlibz.Results.Mcp icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Results/main/docs/icon.png)

# Hlibz.Results.Mcp

[![NuGet](https://img.shields.io/nuget/v/Hlibz.Results.Mcp.svg)](https://www.nuget.org/packages/Hlibz.Results.Mcp)
[![CI](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml)

Turns [Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results) errors into MCP tool errors
a model can act on. An error's message says what failed, for your logs; **guidance** says what
to do next, in terms of your tools, for the model.

```bash
dotnet add package Hlibz.Results.Mcp
```

```csharp
[McpServerTool(Name = "delete_city")]
[ToolErrors<DeleteCityError>]   // the error set the tool's result declares
[ToolError<ValidationFailed>]   // an ambient error it can fail with too
public static async Task<Unit> DeleteAsync(CityService cities, Guid cityId, CancellationToken ct) =>
    (await cities.DeleteAsync(cityId, ct)).ValueOrThrow();

builder.Services.AddMcpServer()
    .WithToolsFromAssembly()
    .WithToolErrors(errors => errors
        .Guide<CityNotFound>("No city has id {cityId}. Call list_cities to find its id.")
        .Guide<CityInUse>("{userCount} users live in the city. Ask the user whether to move them first.")
        .Guide<ValidationFailed>("Fix these arguments and call the tool again: {errors}"));
```

A failed call comes back as a tool error, its placeholders filled from the error's data:

```json
{
  "isError": true,
  "content": [{ "type": "text", "text": "city_in_use: 3 users live in the city. Ask the user whether to move them first." }],
  "structuredContent": { "code": "city_in_use", "details": { "cityId": "6f1c…", "userCount": 3 } }
}
```

- **Each tool lists its errors** in its `_meta`, under `hlibz/errors`:
  `[{ "code", "description", "guidance" }]`, where the description is the error's declared
  message. Any client can read them, and
  [Hlibz.McpDocs.AspNetCore](https://www.nuget.org/packages/Hlibz.McpDocs.AspNetCore) shows them
  on its docs page.
- **The server doesn't start** while a tool's error has no guidance, or guidance names a detail
  its error doesn't have. Set `RequireGuidance = false` to send the message instead.
- **Your own sources:** register an `IToolErrorSource` to work out a tool's errors some other
  way, such as from the use case it runs. Its errors follow the declared ones.
- **Details** are written with the MCP SDK's JSON options, in camelCase like your tools' results.
  A `ValidationFailed`'s keys are converted too: `items[0].unitPrice`.

Tools return their value, which the MCP SDK serializes before any filter sees it, so a tool
unwraps its result with `ValueOrThrow()` (or `ThrowIfFailed()`). Without `WithToolErrors`, the
`ToolErrorException` it throws is still an `McpException`, sent as `"code: message"`.

Works with the MCP C# SDK (ModelContextProtocol) 2.4.0 and later.

Targets .NET 11 (C# 15 unions). .NET 10 apps stay on 2.x.

## A family of packages

One of five packages I build to set up new .NET APIs the way I want them: Hlibz.Results,
Hlibz.UseCases, Hlibz.MinimalApi.Endpoints, Hlibz.OpenApi and Hlibz.Paging. Each works on its own,
and they work best together, as in [Hlibz.Stack](https://github.com/hlib-zinchenko/Hlibz.Stack). New
minor and major versions come often, a new major whenever .NET gives me a better way to do
something. Breaking changes only ever come in a major and are listed first in its
[release notes](https://github.com/hlib-zinchenko/Hlibz.Results/releases). You're welcome to use the
packages, and to [open an issue](https://github.com/hlib-zinchenko/Hlibz.Results/issues) when
something is wrong or missing.

Full documentation and samples: [github.com/hlib-zinchenko/Hlibz.Results](https://github.com/hlib-zinchenko/Hlibz.Results#mcp-tools).
