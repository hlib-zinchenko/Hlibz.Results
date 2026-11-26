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
builder.Services.AddErrorCodeCatalog(typeof(CityNotFound).Assembly);

RouteGroupBuilder cities = app.MapGroup("api/cities").WithResultResponses();
cities.MapDelete("{id:guid}", (Guid id, CityService service) => service.Delete(id));

app.MapErrorCodes(); // GET api/error-codes → [{ "code": "cities_1002", "status": 409, ... }]
```

`WithResultResponses()` lets handlers return a result as is, and documents each error a
`Result<T, TError>` declares: a response per status code, plus `DeclaredErrorsMetadata` for
documentation that shows each error's details. Without it, call `ToHttpResult()`; in a
controller, `ToActionResult()` sends the same status codes and bodies.

A success is `200` unless you configure it per app and HTTP method
(`options.SuccessStatusCodes[HttpMethods.Delete] = 204`), per group or endpoint
(`.WithSuccessStatusCode(201)`, `[SuccessStatusCode(201)]`), or send `201` with a `Location`
(`.WithCreatedLocation<City>(c => $"/api/cities/{c.Id}")`, `ToCreatedHttpResult(...)`). See
[Success status codes](https://github.com/hlib-zinchenko/Hlibz.Results#success-status-codes).

| Status | HTTP (default) |
|---|---|
| `Success` | 200 (configurable) |
| `Error` | 400 |
| `Invalid` | 400 |
| `NotFound` | 404 |
| `Unauthorized` | 401 |
| `Forbidden` | 403 |
| `Conflict` | 409 |
| `Unavailable` | 503 |

Every failure has this body, the error's own data as `details` (`validationErrors` instead for a
`ValidationFailed`):

```json
{
  "message": "The city is used by 2 user profile(s).",
  "code": "cities_1002",
  "details": { "cityId": "1111…", "userCount": 2, "userIds": ["aaaa…", "bbbb…"] }
}
```

Your code says *what* failed; the HTTP code is configuration (422 for `Invalid`, say).
Validation error keys follow your JSON naming policy per path segment: `Items[0].UnitPrice` →
`items[0].unit_price` under snake_case.

For clients that want one shape for every body, success included, there is an opt-in envelope
(`ToEnvelopeHttpResult()`, `WithResultEnvelopes()`, or your own shape), which can also be sent
as HTTP 200 always. The error code catalog publishes every code your app can return, with its
status and the schema of its details, per app if you have several, and with `<ErrorCodesGenerateFiles>true</ErrorCodesGenerateFiles>` in the
app's project every build writes the catalogs to JSON files too, for clients that shouldn't wait
for a running API (set up like ASP.NET Core's build-time OpenAPI documents).

AOT-compatible: `AddResultErrorResponses` adds compile-time JSON metadata for `ErrorResponse`
and `ResultEnvelope`. In a Native AOT app (reflection-based JSON off), add to your
`JsonSerializerContext` each result type a minimal API handler returns (ASP.NET Core asks for
its metadata when it builds the endpoint, even under `WithResultResponses()`), the
`ResultEnvelope<T>`s and values you return, and each error type with data (one missing goes out
without `details`).

## Related packages

- [Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results): the `Result` types.
- [Hlibz.Results.FluentValidation](https://www.nuget.org/packages/Hlibz.Results.FluentValidation):
  a failed validation as a `ValidationFailed` error.
- [Hlibz.MinimalApi.Endpoints](https://www.nuget.org/packages/Hlibz.MinimalApi.Endpoints):
  minimal API endpoints as classes; their handlers return results the same way.

Full documentation and samples: [github.com/hlib-zinchenko/Hlibz.Results](https://github.com/hlib-zinchenko/Hlibz.Results).
