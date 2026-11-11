# Hlibz.Results.AspNetCore

Turns [Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results) into HTTP responses with
**one error body for every failure**: from minimal API endpoints and controllers to the
authorization gate's own 401/403 and anything your middleware writes.

```bash
dotnet add package Hlibz.Results.AspNetCore
```

```json
{ "message": "The order is closed.", "code": "orders_1001", "validation_errors": null }
```

| Status | HTTP (default) |
|---|---|
| `Success` | 200 |
| `Error` | 400 |
| `Invalid` | 400 |
| `NotFound` | 404 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `Conflict` | 409 |
| `Unavailable` | 503 |

Your code says *what* failed; the HTTP code is configuration (e.g. 422 for `Invalid`, see
Options). Validation error keys follow your JSON naming policy per path segment:
`Items[0].UnitPrice` → `items[0].unit_price` under snake_case.

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

Controllers with `[ApiController]` answer model-binding failures (e.g. malformed JSON) with
ASP.NET Core's own `ProblemDetails`, not `ErrorResponse`; set `InvalidModelStateResponseFactory`
if you want those in the same shape.

Full documentation and samples (a minimal API app and a controllers app): [https://github.com/hlib-zinchenko/Hlibz.Results](https://github.com/hlib-zinchenko/Hlibz.Results).
