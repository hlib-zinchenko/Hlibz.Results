![Hlibz.Results icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Results/main/docs/icon.png)

# Hlibz.Results

[![CI](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/blob/main/LICENSE)<br>
[![Hlibz.Results](https://img.shields.io/nuget/v/Hlibz.Results?label=Hlibz.Results)](https://www.nuget.org/packages/Hlibz.Results)
[![downloads](https://img.shields.io/nuget/dt/Hlibz.Results?label=downloads)](https://www.nuget.org/packages/Hlibz.Results)<br>
[![Hlibz.Results.AspNetCore](https://img.shields.io/nuget/v/Hlibz.Results.AspNetCore?label=Hlibz.Results.AspNetCore)](https://www.nuget.org/packages/Hlibz.Results.AspNetCore)
[![downloads](https://img.shields.io/nuget/dt/Hlibz.Results.AspNetCore?label=downloads)](https://www.nuget.org/packages/Hlibz.Results.AspNetCore)<br>
[![Hlibz.Results.FluentValidation](https://img.shields.io/nuget/v/Hlibz.Results.FluentValidation?label=Hlibz.Results.FluentValidation)](https://www.nuget.org/packages/Hlibz.Results.FluentValidation)
[![downloads](https://img.shields.io/nuget/dt/Hlibz.Results.FluentValidation?label=downloads)](https://www.nuget.org/packages/Hlibz.Results.FluentValidation)

A small Result pattern for .NET APIs, with **error codes** and **one error body for every
failure**: from your handlers, through minimal API endpoints or controllers, to the
authorization gate's own 401/403 and anything your middleware writes.

| Package | What it gives you |
|---|---|
| `Hlibz.Results` | `Result` / `Result<T>`, `ResultStatus`, `ErrorCode`, `Map`/`Bind`, `ErrorCodeCatalog`. No dependencies. |
| `Hlibz.Results.AspNetCore` | `ToHttpResult()` (minimal APIs), `ToActionResult()` (controllers), `ErrorResponse`, the optional envelope, the auth gate's error body, `WriteErrorAsync`, `MapErrorCodes`. |
| `Hlibz.Results.FluentValidation` | `validationResult.ToErrorResult()` / `validationResult.ToInvalidResult()`. |

Targets .NET 8, 9, 10 and 11.

## Why another Result library?

Ardalis.Result, ErrorOr and FluentResults are all fine. This one is opinionated about the HTTP
side, which is where most of the copy-paste between projects lives:

- Every failure can carry a machine-readable **error code** (`orders_1001`) that clients branch
  on, and the catalog lists every code your API can return (`GET api/error-codes`), failing at
  startup when two share a code.
- Every error, whoever produces it, has the same body:
  `{ "message": "...", "code": "orders_1001" | null, "validation_errors": { ... } }`.
- Your code says *what* failed (`Invalid`, `Conflict`, ...); the HTTP code is configuration, so
  400-only and 400/422 teams both fit.
- Validation error keys follow your JSON naming policy per path segment:
  `Items[0].UnitPrice` → `items[0].unit_price` under snake_case.

## Results

```csharp
using Hlibz.Results;

public sealed record OrdersErrorCode(string Code, string Message)
    : ErrorCode($"orders_{Code}", Message);

public static class OrdersErrorCodes
{
    public static readonly ErrorCode OrderClosed =
        new OrdersErrorCode("1001", "The order is closed.");
}

public async Task<Result<OrderDto>> AddLineAsync(Guid orderId, AddLine line)
{
    Order? order = await _orders.GetAsync(orderId);
    if (order is null)
    {
        return Result<OrderDto>.NotFound();
    }

    if (order.IsClosed)
    {
        return Result<OrderDto>.Conflict(OrdersErrorCodes.OrderClosed);
    }

    Result added = order.AddLine(line);
    if (added.IsFailure)
    {
        return Result<OrderDto>.FromFailed(added);
    }

    return Result<OrderDto>.Success(order.ToDto());
}
```

| Status | Factory | HTTP (default) |
|---|---|---|
| `Success` | `Success()` / `Success(value)` | 200 |
| `Error` | `Error(message)`, `Error(errorCode)`, `Error(validationErrors)`, `validationResult.ToErrorResult()` | 400 |
| `Invalid` | `Invalid(validationErrors)`, `validationResult.ToInvalidResult()` | 400 |
| `NotFound` | `NotFound(message?)`, `NotFound(errorCode)` | 404 |
| `Unauthorized` | `Unauthorized(message?)`, `Unauthorized(errorCode)` | 401 |
| `Forbidden` | `Forbidden(message?)`, `Forbidden(errorCode)` | 403 |
| `Conflict` | `Conflict(message?)`, `Conflict(errorCode)` | 409 |
| `Unavailable` | `Unavailable(message?)`, `Unavailable(errorCode)` | 503 |

Your code says *what* failed; the HTTP layer picks the number. `Error` is a broken business rule
or another bad request, `Invalid` a failed input validation, so an API can keep both at 400 or
send 422 for one or both (see Options). Validation errors can ride on either: pick `Error` if
your API doesn't tell validation apart from other bad requests, `Invalid` if it does. To keep a
codebase to one of them, ban the other's factories with
[BannedApiAnalyzers](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers).

`Result<T>` is a `Result`, and `IsSuccess` tells the compiler `Value` isn't null.
`FromFailed` passes a failure on as another type; `Map`, `MapAsync` and `Bind` chain steps.

## Minimal APIs

```csharp
builder.Services.AddResultErrorResponses();
builder.Services.AddErrorCodeCatalog(typeof(OrdersErrorCodes).Assembly);

app.MapPost("orders/{id:guid}/lines", async (Guid id, AddLine line, IOrderService orders) =>
    (await orders.AddLineAsync(id, line)).ToHttpResult());

app.MapErrorCodes(); // GET api/error-codes → [{ "code": "orders_1001", "message": "..." }]
```

### Error codes per app

Catalogs are built per assembly, so a customer app and an admin app can each publish only their
own codes:

```csharp
builder.Services
    .AddErrorCodeCatalog(typeof(OrdersErrorCodes).Assembly)        // default catalog
    .AddErrorCodeCatalog("admin",                                  // named catalog
        typeof(OrdersErrorCodes).Assembly,
        typeof(AdminErrorCodes).Assembly);

app.MapErrorCodes();                                               // api/error-codes
app.MapErrorCodes("api/admin/error-codes", "admin").RequireAuthorization("Admin");
```

Each catalog rejects duplicate codes at startup; the same assembly may be in several. From a
controller, inject `IErrorCodeCatalog` for the default one and
`[FromKeyedServices("admin")] IErrorCodeCatalog` for a named one.

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

### Returning results as they are

In a minimal API, `WithResultEnvelopes()` on an endpoint or a route group lets handlers return
a `Result` or `Result<T>` (or a `Task` of one) with no conversion call. Each result is sent as
`ToEnvelopeHttpResult()` would send it, and the success response is documented in OpenAPI as
`200 ResultEnvelope<T>` instead of the `Result<T>` ASP.NET Core would infer:

```csharp
RouteGroupBuilder orders = app.MapGroup("/api/orders").WithResultEnvelopes();

orders.MapGet("{id:int}", (int id, OrderService service) => service.Find(id));  // Result<OrderDto>
orders.MapDelete("{id:int}", (int id, OrderService service) => service.Delete(id));  // Result
```

Handlers that return anything else are left alone, so it's safe on a whole group. Document the
failures on the endpoint (`.Produces<ResultEnvelope>(404)`). With your own envelope (below), the
`200` is documented without a type, since its shape is yours.

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
{ "data": null, "is_success": false, "status": 404, "message": "Resource was not found.", "code": null }
```

It only affects envelopes: a plain `ErrorResponse` has no status of its own, so `ToHttpResult()`
keeps the real code. The auth gate still sets `WWW-Authenticate` on what is now a `200`.

## Options

```csharp
builder.Services.AddResultErrorResponses(options =>
{
    // 400 vs 422: pick your team's convention.
    options.StatusCodes[ResultStatus.Invalid] = StatusCodes.Status422UnprocessableEntity;

    options.ValidationKeyNaming = ValidationKeyNaming.AsIs; // default: JsonNamingPolicy
    options.FormatValidationKey = key => key.ToUpperInvariant(); // or your own
    options.Messages.NotFound = "Nothing here.";              // default messages per status
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

## Errors outside endpoints

```csharp
context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
await context.Response.WriteErrorAsync("Too many requests.", code: null, ct);

await context.Response.WriteErrorAsync(Result.Conflict("Someone else changed it."), ct);
```

## FluentValidation

```csharp
using Hlibz.Results.FluentValidation;

ValidationResult validation = await validator.ValidateAsync(request);
if (!validation.IsValid)
{
    return validation.ToInvalidResult(); // status Invalid; ToInvalidResult<T>() for Result<T>
    // or: return validation.ToErrorResult(); // status Error, same validation_errors
}
```

`validation.ToErrors()` gives the raw errors dictionary if you build the failure yourself.

## Samples

[`samples/`](samples/README.md) has a small shop as a minimal API app and a controllers app,
with customer and admin error codes in separate assemblies, and a `.http` file for every
failure.

Controllers with `[ApiController]` answer model-binding failures (e.g. malformed JSON) with
ASP.NET Core's own `ProblemDetails`, not `ErrorResponse`; set `InvalidModelStateResponseFactory`
if you want those in the same shape.

## Versioning

The three packages are released together under one version from one `vX.Y.Z` tag, so matching
versions always go together. Dependencies are declared as minimums (FluentValidation 11.0.0 and
later), so a new FluentValidation or ASP.NET Core release needs no new Hlibz.Results release:
reference the newer version in your app and NuGet uses it.

## License

MIT
