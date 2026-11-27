![Hlibz.Results icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Results/main/docs/icon.png)

# Hlibz.Results

[![CI](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/blob/main/LICENSE)<br>
[![Hlibz.Results](https://img.shields.io/nuget/v/Hlibz.Results?label=Hlibz.Results)](https://www.nuget.org/packages/Hlibz.Results)
[![downloads](https://img.shields.io/nuget/dt/Hlibz.Results?label=downloads)](https://www.nuget.org/packages/Hlibz.Results)<br>
[![Hlibz.Results.AspNetCore](https://img.shields.io/nuget/v/Hlibz.Results.AspNetCore?label=Hlibz.Results.AspNetCore)](https://www.nuget.org/packages/Hlibz.Results.AspNetCore)
[![downloads](https://img.shields.io/nuget/dt/Hlibz.Results.AspNetCore?label=downloads)](https://www.nuget.org/packages/Hlibz.Results.AspNetCore)<br>
[![Hlibz.Results.FluentValidation](https://img.shields.io/nuget/v/Hlibz.Results.FluentValidation?label=Hlibz.Results.FluentValidation)](https://www.nuget.org/packages/Hlibz.Results.FluentValidation)
[![downloads](https://img.shields.io/nuget/dt/Hlibz.Results.FluentValidation?label=downloads)](https://www.nuget.org/packages/Hlibz.Results.FluentValidation)<br>
[![Hlibz.Results.Mcp](https://img.shields.io/nuget/v/Hlibz.Results.Mcp?label=Hlibz.Results.Mcp)](https://www.nuget.org/packages/Hlibz.Results.Mcp)
[![downloads](https://img.shields.io/nuget/dt/Hlibz.Results.Mcp?label=downloads)](https://www.nuget.org/packages/Hlibz.Results.Mcp)

A Result pattern for .NET APIs with **typed errors**: every failure is a record carrying the data
a client needs to act on it, an operation declares the errors it returns as a C# 15 `union`, and
documentation lists each one with its data. One error body for every failure: from your
handlers, through minimal API endpoints or controllers, to the authorization gate's own 401/403
and anything your middleware writes. MCP tools fail the same way, with guidance for the model.

| Package | What it gives you |
|---|---|
| `Hlibz.Results` | `Result`, `Result<T>`, `Result<T, TError>`, `Error` + `[Error]`, the ambient errors, `Map`/`Bind`, `ErrorCodeCatalog`, and the source generator and analyzer behind them. No dependencies. |
| `Hlibz.Results.AspNetCore` | `ToHttpResult()` (minimal APIs), `ToActionResult()` (controllers), `ErrorResponse` with the error's `details`, each declared error documented per endpoint, the optional envelope, the auth gate's error body, `WriteErrorAsync`, `MapErrorCodes`. |
| `Hlibz.Results.FluentValidation` | `validationResult.ToValidationFailed()`. |
| `Hlibz.Results.Mcp` | MCP tool errors: `ValueOrThrow()` in a tool, `WithToolErrors(...)` with guidance per error, `[ToolErrors<TErrorSet>]`, each tool's errors in its `_meta`. |

Targets .NET 11 (C# 15 unions). .NET 10 apps stay on 2.x.

## A family of packages

Hlibz.Results is one of five packages I build to set up new .NET APIs the way I want them:
**Hlibz.Results**, [Hlibz.UseCases](https://github.com/hlib-zinchenko/Hlibz.UseCases),
[Hlibz.MinimalApi.Endpoints](https://github.com/hlib-zinchenko/Hlibz.MinimalApi.Endpoints),
[Hlibz.OpenApi](https://github.com/hlib-zinchenko/Hlibz.OpenApi) and
[Hlibz.Paging](https://github.com/hlib-zinchenko/Hlibz.Paging). Each one works on its own, and they
work best together: [Hlibz.Stack](https://github.com/hlib-zinchenko/Hlibz.Stack) is an API where all
five meet, along with [Hlibz.Redoc](https://github.com/hlib-zinchenko/Hlibz.Redoc) and
[Hlibz.McpDocs.AspNetCore](https://github.com/hlib-zinchenko/Hlibz.McpDocs.AspNetCore).

Because they follow what my projects need, new minor and major versions come often. When .NET gives
me a better way to do something, it goes into a new major straight away: Hlibz.Results 2.0 came a
week after 1.0, and the first 3.0 preview, built on C# 15 unions, nine days after 2.0. Breaking
changes only ever come in a major, its
[release notes](https://github.com/hlib-zinchenko/Hlibz.Results/releases) list them first, and older
versions stay on NuGet, so read the notes before you move up a major.

You're welcome to use them, and to
[open an issue](https://github.com/hlib-zinchenko/Hlibz.Results/issues) when something is wrong or
missing.

## Why another Result library?

Ardalis.Result, ErrorOr and FluentResults are all fine. This one is opinionated about errors as
data and about the HTTP side, which is where most of the copy-paste between projects lives:

- **An error is a type with data.** Deleting a city that user profiles still use fails with
  `CityInUse(CityId, UserCount, UserIds)`, and the client gets the ids in the error's `details`
  instead of asking again.
- **An operation declares its errors.** `Result<Unit, DeleteCityError>` where
  `union DeleteCityError(CityNotFound, CityInUse)`: callers switch over them exhaustively, the
  build rejects an error the set doesn't list, and each endpoint's OpenAPI responses list them.
- **Codes clients branch on** (`cities_1002`), listed by a catalog (`GET api/error-codes`, with
  each code's status and details schema) that a source generator builds at compile time,
  rejecting two errors that share a code as a build error.
- Your code says *what* failed (`Conflict`, `Invalid`, ...); the HTTP code is configuration, so
  400-only and 400/422 teams both fit. Validation error keys follow your JSON naming policy per
  path segment: `Items[0].UnitPrice` → `items[0].unit_price` under snake_case.

## Errors

An error is a `sealed record` deriving from `Error`, with an `[Error]` attribute giving its code,
what failed and its message, and whatever data a client needs:

```csharp
using Hlibz.Results;

[Error("cities_1001", ResultStatus.NotFound, "There's no city with this id.")]
public sealed record CityNotFound(Guid CityId) : Error;

[Error("cities_1002", ResultStatus.Conflict, "The city is used by user profiles.")]
public sealed record CityInUse(Guid CityId, int UserCount, IReadOnlyList<Guid> UserIds) : Error
{
    // Optional: word the message from the data.
    protected override string FormatMessage(string message) =>
        $"The city is used by {UserCount} user profile(s).";
}
```

`error.Code`, `error.Status` and `error.Message` come from the attribute; everything else is the
error's details. Return an error where a result is expected: it converts.

| `ResultStatus` | Meaning | HTTP (default) |
|---|---|---|
| `Success` | | 200 ([configurable](#success-status-codes)) |
| `Error` | A broken business rule or another bad request | 400 |
| `Invalid` | The input failed validation | 400 |
| `NotFound` | What the request refers to doesn't exist | 404 |
| `Unauthorized` | Not authenticated | 401 |
| `Forbidden` | Authenticated but not allowed | 403 |
| `Conflict` | Conflicts with the current state | 409 |
| `Unavailable` | A dependency is down; retry later | 503 |
| `TooManyRequests` | Too many requests (a rate limit); retry later | 429 |

### Ambient errors

Errors any operation can fail with, added by the layers around it (validation, authorization, a
filter, a rate limiter) rather than declared by it, so every result accepts them whatever its
error set:

| Error | Code | Status | Typically from |
|---|---|---|---|
| `ValidationFailed(Errors)` | `validation_failed` | `Invalid` (400) | validation |
| `Unauthorized` | `unauthorized` | `Unauthorized` (401) | the auth gate |
| `Forbidden` | `forbidden` | `Forbidden` (403) | the auth gate, an authorization filter |
| `ConcurrencyConflict` | `concurrency_conflict` | `Conflict` (409) | an optimistic concurrency check (EF Core's `DbUpdateConcurrencyException`, say) |
| `RateLimited` | `rate_limited` | `TooManyRequests` (429) | a rate limiter's `OnRejected` |
| `Unavailable` | `unavailable` | `Unavailable` (503) | a dependency that is down |

They derive from `AmbientError`, a `closed` record: these are all of them, so the catalog lists
them and a client can handle each. An error of the host's own that no operation returns (a
maintenance mode, say) is an ordinary `[Error]` record: the catalog lists it too, and
`WriteErrorAsync` writes it.

## Results

```csharp
public union GetCityError(CityNotFound);
public union DeleteCityError(CityNotFound, CityInUse);

public Result<City, GetCityError> Get(Guid id) =>
    _cities.TryGetValue(id, out City? city) ? city : new CityNotFound(id);

public Result<Unit, DeleteCityError> Delete(Guid id)
{
    Result<City, GetCityError> found = Get(id);
    if (found.IsFailure)
    {
        return found.Error;          // a smaller set's error passes on into a larger one
    }

    Guid[] users = UsersOf(id);
    if (users.Length > 0)
    {
        return new CityInUse(id, users.Length, [.. users.Take(50)]);
    }

    _cities.Remove(id);
    return Unit.Value;
}
```

| Type | Fails with | Use it for |
|---|---|---|
| `Result` | any `Error` | an operation that returns nothing |
| `Result<T>` | any `Error` | a value, errors undeclared |
| `Result<T, TError>` | an error of `TError`, or an ambient one | a value (`Unit` for none) and the errors it declares |

`TError` is an **error set**: a non-generic `union` whose cases are all error types, none of
them ambient. A caller switches over `DeclaredError` exhaustively, with no `_` arm, so adding an
error to the set flags every switch that doesn't handle it yet:

```csharp
Result<Unit, DeleteCityError> deleted = service.Delete(id);
string text = deleted.DeclaredError switch
{
    CityNotFound => "No such city.",
    CityInUse inUse => $"Used by {inUse.UserCount} profiles.",
    null => deleted.IsSuccess ? "Deleted." : deleted.Error.Message,   // success or ambient
};
```

`Result<T, TError>` is a `Result<T>`, which is a `Result`, so anything taking a plain result
takes it. `IsSuccess`/`IsFailure` tell the compiler whether `Value` or `Error` is set; `Map`,
`MapAsync` and `Bind` chain steps, keeping the error set. A `union` compiles to a struct without
equality members, which CA1815 flags under `AnalysisMode=All`; Hlibz.Results suppresses it on
error sets (`HRESSPR1815`), which are never compared, and leaves it on every other struct.

Nothing is scanned at runtime: Hlibz.Results' source generator lists each assembly's error types
and error sets as it compiles, and the analyzer reports what would otherwise fail at runtime
(see [Build errors](#build-errors)), such as returning `CityArchived` from an operation whose set
doesn't list it. An error passed on as a plain `Error` can't be checked at build time, and throws
`ArgumentException` if the set doesn't list it.

## Minimal APIs

```csharp
builder.Services.AddResultErrorResponses();
builder.Services.AddErrorCodeCatalog(typeof(CityNotFound).Assembly);

app.MapDelete("api/cities/{id:guid}", (Guid id, CityService cities) =>
    cities.Delete(id).ToHttpResult());

app.MapErrorCodes(); // GET api/error-codes → [{ "code": "cities_1002", "status": 409, ... }]
```

A failure goes out as its status code and an `ErrorResponse`, with the error's data as
`details` (left out of an error without data):

```json
{
  "message": "The city is used by 2 user profile(s).",
  "code": "cities_1002",
  "details": { "cityId": "1111…", "userCount": 2, "userIds": ["aaaa…", "bbbb…"] }
}
```

A `ValidationFailed` sends its messages as `validationErrors` instead, and a
`Result<Unit, TError>` succeeds with no body, as a plain `Result` does.

### Returning results as they are

`WithResultResponses()` on an endpoint or a route group lets handlers return a `Result` or
`Result<T>` (or a `Task` of one) with no `ToHttpResult()` call. Each result is sent as
`ToHttpResult()` would send it, and the success response is documented in OpenAPI as it is sent,
`200 T` by default (no body for a plain `Result`), instead of the `Result<T>` ASP.NET Core would
infer:

```csharp
RouteGroupBuilder orders = app.MapGroup("/api/orders").WithResultResponses();

orders.MapGet("{id:int}", (int id, OrderService service) => service.Find(id));  // Result<OrderDto>
orders.MapDelete("{id:int}", (int id, OrderService service) => service.Delete(id));  // Result
```

Handlers that return anything else are left alone, so it's safe on a whole group, and a response
a filter sends in place of the handler's (a validation failure, say) goes out as it is.

A handler returning a `Result<T, TError>` gets its errors documented too: one response per status
code they are sent with (`404 ErrorResponse`, `409 ErrorResponse`), unless the endpoint
documents that status itself, and every error in the endpoint's `DeclaredErrorsMetadata`, with
the status it goes out with, for documentation that shows each error's code and details schema
(Hlibz.OpenApi.Results does). Ambient errors aren't documented here: validation and
authorization are documented by what adds them. Document a plain `Result<T>`'s failures on the
endpoint (`.Produces<ErrorResponse>(404)`).

A handler that can't return the `Result<T, TError>`, because it returns an `IResult` (a file, or
a result it sends itself with `ToHttpResult()`) or a filter wraps what it returns, declares the
set with `ProducesErrorSet<TErrorSet>()`. It's documented the same way, as `ResultEnvelope`s when
`ResultHttpOptions.UseEnvelope` is on. Documentation only: how the errors go out is the handler's
business. A handler that does return a `Result<T, TError>` can declare another set beside it.

```csharp
cities.MapGet("{id:guid}/export", ExportCity)  // IResult: a CSV file, or the error
    .ProducesErrorSet<GetCityError>();
```

### Success status codes

A success is `200` unless you say otherwise, at three levels; the most specific wins:

```csharp
// App: by HTTP method (a method without an entry uses StatusCodes[ResultStatus.Success], 200).
builder.Services.AddResultErrorResponses(options =>
    options.SuccessStatusCodes[HttpMethods.Delete] = StatusCodes.Status204NoContent);

// Group: every endpoint in it.
RouteGroupBuilder imports = app.MapGroup("/api/imports")
    .WithResultResponses()
    .WithSuccessStatusCode(StatusCodes.Status202Accepted);

// Endpoint: beats its group (an inner group beats an outer one) and the app.
RouteGroupBuilder orders = app.MapGroup("/api/orders").WithResultResponses();
orders.MapPost("", (OrderService s) => s.Create())                          // 201 + Location
    .WithCreatedLocation<OrderDto>(order => $"/api/orders/{order.Id}");
orders.MapPost("{id}/archive", (Guid id, OrderService s) => s.Archive(id))  // 201, no Location
    .WithSuccessStatusCode(StatusCodes.Status201Created);
orders.MapDelete("{id}", (Guid id, OrderService s) => s.Delete(id));        // 204, from the app
orders.MapPost("{id}/pay", (Guid id, OrderService s) => s.Pay(id));         // 200
```

- `WithResultResponses()`/`WithResultEnvelopes()` send and document the code the endpoint
  resolves to. `WithSuccessStatusCode` and `WithCreatedLocation` work with direct
  `ToHttpResult()` calls too, but then you document the response yourself.
- `[SuccessStatusCode(201)]` does the same as an attribute: on a minimal API handler method, a
  controller action, or a controller class (an action's beats its controller's).
- For one call, `ToCreatedHttpResult(order => $"/api/orders/{order.Id}")` sends `201` with that
  `Location`, as do `ToCreatedEnvelopeHttpResult`, `ToCreatedActionResult` and
  `ToCreatedEnvelopeActionResult`. A `null` location leaves the header out. Use these for
  conditional codes, e.g. an upsert: `created ? r.ToCreatedHttpResult(...) : r.ToHttpResult()`.
- A `204` has no body, so a `Result<T>`'s value is dropped. An envelope always has one: an
  envelope endpoint that resolves to `204` fails when it's built (or, for a direct
  `ToEnvelope…` call, when it's sent).
- Failures are unaffected, and every success code must be `2xx`.

### Error codes per app

Catalogs are built per assembly, so a customer app and an admin app can each publish only their
own codes:

```csharp
builder.Services
    .AddErrorCodeCatalog(typeof(CityNotFound).Assembly)            // default catalog
    .AddErrorCodeCatalog("admin",                                  // named catalog
        typeof(CityNotFound).Assembly,
        typeof(AdminOnly).Assembly);

app.MapErrorCodes();                                               // api/error-codes
app.MapErrorCodes("api/admin/error-codes", "admin").RequireAuthorization("Admin");
```

Each catalog lists the ambient errors too, and rejects two assemblies sharing a code at startup
(two errors sharing a code in one assembly is a build error); the same assembly may be in several
catalogs. Each entry is the error's code, the HTTP status it goes out with, its message and the
JSON schema of its `details`, as the app's JSON options write it. From a controller, inject
`IErrorCodeCatalog` for the default one and `[FromKeyedServices("admin")] IErrorCodeCatalog` for
a named one.

An assembly that references Hlibz.Results but was compiled without the generator makes the
catalog throw; one that doesn't reference it adds no codes.

### Error code files at build time

Front ends and other clients needn't wait for a running API to get the codes: the build can
write each catalog to a JSON file, as the list `MapErrorCodes` serves (same JSON options,
indented). It's set up like ASP.NET Core's build-time OpenAPI documents
(`Microsoft.Extensions.ApiDescription.Server`), property for property:

| Error code files | OpenAPI documents | What it does |
|---|---|---|
| `ErrorCodesGenerateFiles` | `OpenApiGenerateDocuments` | Turns the feature on. **Off by default** (see below). |
| `ErrorCodesGenerateFilesOnBuild` | `OpenApiGenerateDocumentsOnBuild` | Writes them after every build; defaults to the above. |
| `ErrorCodesDirectory` | `OpenApiDocumentsDirectory` | Where they go, relative to the project folder; `obj/` by default. |
| `ErrorCodesGenerateFilesOptions` | `OpenApiGenerateDocumentsOptions` | Options for the tool: `--environment <Name>`. |
| `GenerateErrorCodeFiles` target | `GenerateOpenApiDocuments` target | Writes them on demand. |

```xml
<PropertyGroup>
  <ErrorCodesGenerateFiles>true</ErrorCodesGenerateFiles>
  <ErrorCodesDirectory>$(MSBuildProjectDirectory)/../../contracts</ErrorCodesDirectory>
</PropertyGroup>
```

The one difference: referencing the OpenAPI package is itself the opt-in, while this package is
in every app that returns results, whose builds shouldn't start running the app unasked.

- `error-codes.json` holds the default catalog, `error-codes.{name}.json` each named one
  (`error-codes.admin.json`). A name a file name can't hold (`/`, `:` and the like) fails the
  build. Files of a catalog you remove are left as they are.
- It runs the app's `Main` until it builds its host, then stops it there, as the OpenAPI tool
  does: code before `builder.Build()` runs (keep it free of what only exists in production, such
  as secrets), no server starts. The environment is `Production` unless the build sets
  `ASPNETCORE_ENVIRONMENT`, or `--environment Development` (any name) is in
  `ErrorCodesGenerateFilesOptions`: a way round startup that needs config only `Development`
  has, as with the OpenAPI tool's option of the same name.
- A file is only rewritten when its content changes, and the target is skipped when the app, its
  copied assemblies and the project file are unchanged. Only a project with one target framework
  writes them on build; with several, run `dotnet msbuild -t:GenerateErrorCodeFiles
  -p:TargetFramework=net11.0`.
- To skip them in one build, CI's say: `dotnet build -p:ErrorCodesGenerateFilesOnBuild=false`.
- With clients in other repositories, write the files into the API's own repository and commit
  them: a changed code shows in review, CI can fail on stale ones with `git diff --exit-code`
  after the build, and each client copies what it needs from there, from a checkout or from
  GitHub, with no API running.

## Controllers

```csharp
[HttpPost("{id:guid}/lines")]
public async Task<ActionResult<OrderDto>> AddLine(Guid id, AddLine line) =>
    (await _orders.AddLineAsync(id, line)).ToActionResult();
```

Same status codes and bodies; keys are cased by MVC's JSON options.

## Envelope (opt-in)

For clients that want one shape for every body, success included:

```csharp
return result.ToEnvelopeHttpResult();   // or ToEnvelopeActionResult()
```

```json
{ "data": { ... }, "is_success": true, "status": 200, "message": "Success.", "code": null }
```

Set `options.UseEnvelope = true` in `AddResultErrorResponses` so the auth gate and
`WriteErrorAsync` use it too.

### Returning results as envelopes

In a minimal API, `WithResultEnvelopes()` is `WithResultResponses()` for envelopes: handlers
return a `Result` or `Result<T>`, each is sent as `ToEnvelopeHttpResult()` would send it, and the
success response is documented as `200 ResultEnvelope<T>` (or its
[success status code](#success-status-codes)):

```csharp
RouteGroupBuilder orders = app.MapGroup("/api/orders").WithResultEnvelopes();

orders.MapGet("{id:int}", (int id, OrderService service) => service.Find(id));  // Result<OrderDto>
```

Document the failures on the endpoint (`.Produces<ResultEnvelope>(404)`). With your own envelope
(below), the `200` is documented without a type, since its shape is yours.

### Your own envelope

If your clients expect a different shape, build it from `EnvelopeContent` (status code, the
`ResultStatus`, message, code, validation errors and data):

```csharp
builder.Services.AddResultErrorResponses(options =>
{
    options.UseEnvelope = true;
    options.CreateEnvelope = content => new ApiEnvelope(
        Ok: content.IsSuccess,
        Error: content.IsSuccess ? null : new ApiError(content.Code, content.Message),
        Payload: content.Data);
});
```

The object is serialized by its runtime type with your JSON settings. `ResultStatus` is `null`
for errors written outside an endpoint (the auth gate, `WriteErrorAsync`). The `ToEnvelope…`
methods still declare the built-in envelope type, so describe the custom one in OpenAPI yourself
(`.Produces<ApiEnvelope>()` or `[ProducesResponseType]`).

### Always HTTP 200

Some clients read the outcome only from the body. `EnvelopeAlwaysOk` sends every envelope
response as `200`, failures included, and keeps the real status in the envelope:

```csharp
options.UseEnvelope = true;      // so the auth gate's 401/403 are enveloped (and 200) too
options.EnvelopeAlwaysOk = true;
```

```json
{ "data": null, "is_success": false, "status": 404, "message": "There's no city with this id.", "code": "cities_1001" }
```

It only affects envelopes: a plain `ErrorResponse` has no status of its own, so `ToHttpResult()`
keeps the real code. The auth gate still sets `WWW-Authenticate` on what is now a `200`.

## Options

```csharp
builder.Services.AddResultErrorResponses(options =>
{
    // 400 vs 422: pick your team's convention.
    options.StatusCodes[ResultStatus.Invalid] = StatusCodes.Status422UnprocessableEntity;

    // Success codes by HTTP method; groups and endpoints can override them.
    options.SuccessStatusCodes[HttpMethods.Delete] = StatusCodes.Status204NoContent;

    options.ValidationKeyNaming = ValidationKeyNaming.AsIs; // default: JsonNamingPolicy
    options.FormatValidationKey = key => key.ToUpperInvariant(); // or your own
    options.SuccessMessage = "Success.";                      // a successful envelope's message
    options.UseEnvelope = false;
    options.CreateEnvelope = null;                            // your own envelope shape
    options.EnvelopeAlwaysOk = false;                         // envelopes always HTTP 200
});
```

| Convention | `Invalid` | `Error` |
|---|---|---|
| Everything is 400 (default) | 400 | 400 |
| 422 for validation only | 422 | 400 |
| 400 = unreadable request, 422 = readable but wrong | 422 | 422 |

The envelope's `status` follows the same mapping, so it always matches the response.

`AddResultErrorResponses` also registers an `IAuthorizationMiddlewareResultHandler`, so a
missing/invalid token (401) or a failed policy (403) gets the same body. It replaces any other
handler you registered.

## MCP tools

Hlibz.Results.Mcp turns errors into MCP tool errors a model can act on. MCP has no error shape: a
failed call is `isError` with text, so a model reads the text and guesses. Here the text is
**guidance**: what to do next, in terms of your tools, with the error's data filled in. The
error's message says what failed, for your logs. A tool's definition lists the errors it can
return, so a model (or a docs page) knows them before it calls.

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

A failed `delete_city`:

```json
{
  "isError": true,
  "content": [{ "type": "text", "text": "city_in_use: 3 users live in the city. Ask the user whether to move them first." }],
  "structuredContent": { "code": "city_in_use", "details": { "cityId": "6f1c…", "userCount": 3 } }
}
```

- **Unwrapping.** MCP tools return their value, which the SDK serializes before any filter sees
  it, so a tool can't return the result itself: `ValueOrThrow()` (or `ThrowIfFailed()` on a plain
  `Result`) throws a `ToolErrorException`, which `WithToolErrors` turns into the error above.
  Without `WithToolErrors` it is still an `McpException`, sent as `"code: message"`.
- **Guidance.** `{name}` placeholders are the error's details as they go out (`{cityId}`); a
  `ValidationFailed`'s one detail is `{errors}`, written as `key: message; key: message`. An
  error without guidance goes out with its message.
- **Declaring a tool's errors.** `[ToolErrors<TErrorSet>]` (an error set) and
  `[ToolError<TError>]` (one error: an ambient one such as `ValidationFailed` or `Forbidden`, or
  one of a tool unwrapping a plain `Result<T>`), as many as it needs. An `IToolErrorSource`
  service works out more, such as the errors of the use case a tool runs; its errors follow the
  declared ones, each listed once.
- **`_meta`.** Each tool with errors lists them under `hlibz/errors` (`ToolErrorOptions.MetaKey`):
  `[{ "code", "description", "guidance" }]`, the description being the error's declared message.
  [Hlibz.McpDocs.AspNetCore](https://www.nuget.org/packages/Hlibz.McpDocs.AspNetCore) reads the
  same key and lists them on the tool, with the guidance; neither package references the other.
- **Checked at start.** The host doesn't start (an `OptionsValidationException` naming each
  problem) while a tool's error has no guidance, a guidance names a detail its error doesn't
  have, or a source lists a type that isn't an error. `RequireGuidance = false` allows errors
  without guidance.
- **JSON.** Details are written with `ToolErrorOptions.SerializerOptions`, by default the MCP
  SDK's (`McpJsonUtilities.DefaultOptions`, camelCase) like your tools' results; a
  `ValidationFailed`'s keys are converted with its naming policy, segment by segment
  (`items[0].unitPrice`). Under Native AOT, an error type those options can't write goes out
  without details.

Works with the MCP C# SDK (ModelContextProtocol) 2.4.0 and later.

## Errors outside endpoints

```csharp
builder.Services.AddRateLimiter(limiter =>
{
    limiter.OnRejected = (context, ct) =>   // 429, rate_limited
        new ValueTask(context.HttpContext.Response.WriteErrorAsync(Result.Failure(new RateLimited()), ct));
});

await context.Response.WriteErrorAsync(Result.Failure(new Unavailable()), ct);   // 503, unavailable
```

`WriteErrorAsync(Result)` sets the status code the error's status maps to and writes its body,
as an endpoint would. `WriteErrorAsync(message, code)` writes a body of your own on the status
code already set; every error body has a code, so it takes one, but prefer an `[Error]` record,
which the catalog lists.

## FluentValidation

```csharp
using Hlibz.Results.FluentValidation;

ValidationResult validation = await validator.ValidateAsync(request);
if (!validation.IsValid)
{
    return validation.ToValidationFailed(); // converts to any result, whatever its error set
}
```

`validation.ToErrors()` gives the raw errors dictionary if you build the failure yourself.

## Build errors

Hlibz.Results' analyzer reports these as you build, each an error since it would otherwise fail
at runtime:

| Id | Meaning |
|---|---|
| `HRES001` | An error type is generic, or nested in a private or protected type: the generated manifest can't name it. |
| `HRES002` | A non-abstract error type has no `[Error]` attribute of its own. |
| `HRES003` | Two error types in the assembly share a code. |
| `HRES004` | A `Result<T, TError>`'s `TError` isn't an error set: a non-generic `union` of error types, none of them ambient. |
| `HRES005` | An error is returned (converted, or passed to `Failure`) where the result's error set doesn't list it. |
| `HRES006` | An `[Error]` has a blank code or the `Success` status. |

It also suppresses CA1815 ("override equals on value types") on error sets, as `HRESSPR1815`: a
union has no equality members of its own, and an error set is never compared. Any other struct
keeps the warning.

## Limitations

- **`[ApiController]` model-binding failures.** Controllers with `[ApiController]` answer them
  (malformed JSON, say) with ASP.NET Core's own `ProblemDetails`, not `ErrorResponse`; set
  `InvalidModelStateResponseFactory` if you want those in the same shape.
- **One authorization result handler.** `AddResultErrorResponses` registers an
  `IAuthorizationMiddlewareResultHandler` for the auth gate's 401/403, which replaces any other
  handler you registered.
- **Your own envelope in OpenAPI.** The `ToEnvelope…` methods declare the built-in envelope
  type, so describe a `CreateEnvelope` shape yourself (`.Produces<ApiEnvelope>()` or
  `[ProducesResponseType]`).
- **Native AOT and reflection-free JSON.** The packages are marked AOT-compatible. The error
  body, the envelopes and the error code list are serialized with the app's JSON options, and
  `AddResultErrorResponses` adds compile-time metadata for `ErrorResponse`, `ResultEnvelope` and
  `IReadOnlyList<ErrorCodeInfo>` to them (after your own resolvers, with your naming policy). In an
  app with reflection-based JSON off (Native AOT, the `webapiaot` template), add to your
  `JsonSerializerContext` each `Result`, `Result<T>` or `Result<T, TError>` a minimal API handler
  returns, each `ResultEnvelope<T>` and value type you return, each error type with data (one
  missing goes out without `details`, and without a schema in the catalog, rather than failing
  the response), and your own `CreateEnvelope` shape. The
  handler's return type is needed even under `WithResultResponses()`/`WithResultEnvelopes()`,
  which turn the result into an `IResult` before anything is written: ASP.NET Core's generated
  endpoint code asks for its metadata when it builds the endpoint, and without it every request
  fails with a `NotSupportedException`.

  ```csharp
  [JsonSerializable(typeof(Result))]
  [JsonSerializable(typeof(Result<OrderDto>))]
  [JsonSerializable(typeof(ResultEnvelope<OrderDto>))]
  internal sealed partial class AppJsonContext : JsonSerializerContext;

  builder.Services.ConfigureHttpJsonOptions(json =>
      json.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));
  ```

  `WithResultResponses()`/`WithResultEnvelopes()` convert a
  `Result<T>` without reflection, value types included. `MapErrorCodes` maps a delegate
  endpoint, so it carries ASP.NET Core's usual trimming warning.
- **Redirecting sign-in schemes.** The auth gate writes its error body only on a 401 or 403. A
  scheme that answers with a redirect instead (cookies: 302 to the login or access denied page)
  keeps its redirect, with no body.

## Samples

[`samples/`](samples/README.md) has a cities API: typed errors with data, error sets, an ambient
validation failure, the documented responses and the error code catalog, with a `.http` file for
every case.

```bash
dotnet run --project samples/Cities.Api   # http://localhost:5180
```

## Versioning

Every package in this repository is released under one version, from one `vX.Y.Z` tag, so matching
versions always go together. Dependencies are declared as minimums (FluentValidation 11.0.0 and
later, the MCP C# SDK 2.4.0 and later), so a new FluentValidation, MCP SDK or ASP.NET Core release
needs no new Hlibz.Results release: reference the newer version in your app and NuGet uses it. 2.0.0
dropped .NET 8 and 9; 3.0.0 targets .NET 11 only and replaces error codes with typed errors
(`ErrorCode`, `Result.NotFound(...)` and the other status factories, `FromFailed`, `ErrorMessage`,
`ValidationErrors` and `ResultMessages` are gone), so .NET 10 apps stay on 2.x.

Release notes: [GitHub Releases](https://github.com/hlib-zinchenko/Hlibz.Results/releases).

## License

MIT
