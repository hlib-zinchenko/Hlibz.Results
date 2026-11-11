# Samples

A small shop, in two web apps over the same use cases:

| Project | What it shows |
|---|---|
| `Shop.Core` | Customer use cases returning `Result`s, entity rules as `Result`s, `Map`/`Bind`/`FromFailed`, FluentValidation → `Invalid`, a dependency failing with `Unavailable`. Error codes `orders_*` and `payments_*`. |
| `Shop.Admin` | Back-office use cases in **their own assembly**, with their own codes `admin_*`, and `Result.Invalid` without FluentValidation. |
| `Shop.MinimalApi` | Minimal APIs with `ToHttpResult()`, real authorization (401/403 bodies from the auth gate), `Invalid` mapped to **422**, the rate limiter's 429 through `WriteErrorAsync`, and two error code catalogs: customers' at `api/error-codes`, admins' at `api/admin/error-codes`. |
| `Shop.Controllers` | Controllers with `ToEnvelopeActionResult()` and `UseEnvelope`, a 401 as a plain failed `Result`, and the catalogs injected (the admin one as a keyed service). |

```bash
dotnet run --project samples/Shop.MinimalApi    # http://localhost:5180
dotnet run --project samples/Shop.Controllers   # http://localhost:5181
```

Each app has a `.http` file with every case, ready for Rider, Visual Studio or VS Code's REST
Client. Sign in to the minimal API with `X-User: alice` + `X-Role: customer` (or
`X-Role: admin`); the controllers app only reads `X-User`.

## Error codes per app

Codes are found by assembly, so how you split assemblies decides who sees what. Here the
customer catalog is built from `Shop.Core` only, and the admin catalog from `Shop.Core` and
`Shop.Admin`:

```csharp
builder.Services
    .AddErrorCodeCatalog(typeof(OrdersErrorCodes).Assembly)                // default
    .AddErrorCodeCatalog("admin",
        typeof(OrdersErrorCodes).Assembly,
        typeof(AdminErrorCodes).Assembly);

app.MapErrorCodes();                                                       // customers
app.MapErrorCodes("api/admin/error-codes", "admin")                        // admins
    .RequireAuthorization("Admin");
```

`GET api/error-codes` (customers):

```json
[
  { "code": "orders_1001", "message": "The order is closed and can't be changed." },
  { "code": "orders_1002", "message": "There isn't enough of this product in stock." },
  { "code": "orders_1003", "message": "The order belongs to another customer." },
  { "code": "orders_1004", "message": "An order needs at least one line before it can be paid." },
  { "code": "payments_1001", "message": "The payment provider is temporarily unavailable. Please retry in a minute." }
]
```

`GET api/admin/error-codes` adds `admin_1001` and `admin_1002`. A code shared by both apps
lives in an assembly both catalogs include; duplicates are only checked within one catalog.

## What each failure looks like (minimal API app)

| Request | Status | Body |
|---|---|---|
| Any customer route without `X-User` | 401 | `{ "message": "Authentication is required.", "code": null }` |
| `api/admin/error-codes` as a customer | 403 | `{ "message": "Access to the resource is forbidden.", "code": null }` |
| Add a line with `{ "sku": "", "quantity": 9 }` | 422 | `validation_errors: { "sku": [...], "quantity": [...] }` |
| Add 4 `TEE` (3 in stock) | 400 | `code: "orders_1002"` |
| Someone else's order | 403 | `code: "orders_1003"` |
| Pay while the provider is down | 503 | `code: "payments_1001"` |
| Pay a paid order | 409 | `code: "orders_1001"` |
| A 4th payment attempt in a minute | 429 | `code: "rate_limited"` |
| Refund an unpaid order (admin) | 409 | `code: "admin_1001"` |

## Other envelope styles

`Shop.Controllers` uses the built-in envelope. For clients that want their own shape, or that
read the outcome only from the body and expect HTTP 200 every time, change its
`AddResultErrorResponses` call:

```csharp
builder.Services.AddResultErrorResponses(options =>
{
    options.UseEnvelope = true;
    options.EnvelopeAlwaysOk = true;          // 200 always; the real status is in the body
    options.CreateEnvelope = content => new   // your own shape
    {
        Success = content.IsSuccess,
        StatusCode = content.StatusCode,
        Error = content.IsSuccess ? null : new { content.Code, content.Message, content.ValidationErrors },
        Result = content.Data,
    };
});
```

The samples reference the library projects; in your app, reference the `Hlibz.Results.*`
packages instead.
