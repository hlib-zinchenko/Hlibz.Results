![Hlibz.Results.AspNetCore icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Results/main/docs/icon.png)

# Hlibz.Results.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/Hlibz.Results.AspNetCore.svg)](https://www.nuget.org/packages/Hlibz.Results.AspNetCore)
[![CI](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml)

Turns [Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results) into HTTP responses with
**one error body for every failure**: from minimal API endpoints and controllers to the
authorization gate's own 401/403 and anything your middleware writes.

```bash
dotnet add package Hlibz.Results.AspNetCore
```

```csharp
builder.Services.AddResultErrorResponses();
builder.Services.AddErrorCodeCatalog(typeof(OrdersErrorCodes).Assembly);

app.MapPost("orders/{id:guid}/lines", async (Guid id, AddLine line, IOrderService orders) =>
    (await orders.AddLineAsync(id, line)).ToHttpResult());

app.MapErrorCodes(); // GET api/error-codes → [{ "code": "orders_1001", "message": "..." }]
```

Or let handlers return a `Result` as is: `app.MapGroup("/api/orders").WithResultResponses()`.
In a controller, `ToActionResult()` sends the same status codes and bodies.

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

Every failure has this body (`validation_errors` only when there are any):

```json
{ "message": "The order is closed.", "code": "orders_1001" }
```

Your code says *what* failed; the HTTP code is configuration (422 for `Invalid`, say).
Validation error keys follow your JSON naming policy per path segment: `Items[0].UnitPrice` →
`items[0].unit_price` under snake_case.

For clients that want one shape for every body, success included, there is an opt-in envelope
(`ToEnvelopeHttpResult()`, `WithResultEnvelopes()`, or your own shape), which can also be sent
as HTTP 200 always. The error code catalog publishes every code your app can return, per app if
you have several.

## Related packages

- [Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results): the `Result` types.
- [Hlibz.Results.FluentValidation](https://www.nuget.org/packages/Hlibz.Results.FluentValidation):
  a failed validation as `Result.Invalid`.
- [Hlibz.MinimalApi.Endpoints](https://www.nuget.org/packages/Hlibz.MinimalApi.Endpoints):
  minimal API endpoints as classes; their handlers return results the same way.

Full documentation and samples: [github.com/hlib-zinchenko/Hlibz.Results](https://github.com/hlib-zinchenko/Hlibz.Results).
