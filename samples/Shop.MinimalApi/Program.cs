using System.Text.Json;
using System.Text.Json.Serialization;

using FluentValidation;

using Hlibz.Results;
using Hlibz.Results.AspNetCore;

using Microsoft.AspNetCore.RateLimiting;

using Shop.Admin;
using Shop.Core.Orders;
using Shop.Core.Payments;
using Shop.MinimalApi;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// snake_case JSON: bodies and validation error keys ("items[0].unit_price") both follow it.
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services
    .AddAuthentication(HeaderAuthenticationHandler.SchemeName)
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions,
        HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Customer, policy => policy.RequireRole("customer"))
    .AddPolicy(Policies.Admin, policy => policy.RequireRole("admin"));

// One error body everywhere, the auth gate's 401/403 included. This app tells validation
// failures apart: Invalid is 422, every other Error stays 400.
builder.Services.AddResultErrorResponses(options =>
    options.StatusCodes[ResultStatus.Invalid] = StatusCodes.Status422UnprocessableEntity);

// Error codes per audience, by assembly: customers see only Shop.Core's codes (orders_,
// payments_); admins also see Shop.Admin's (admin_), which customers must never learn about.
builder.Services
    .AddErrorCodeCatalog(typeof(OrdersErrorCodes).Assembly)
    .AddErrorCodeCatalog(
        "admin",
        typeof(OrdersErrorCodes).Assembly,
        typeof(AdminErrorCodes).Assembly);

// Errors written outside endpoints use the same body: here, the rate limiter's 429.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter(Policies.Payments, window =>
    {
        window.PermitLimit = 3;
        window.Window = TimeSpan.FromMinutes(1);
    });
    options.OnRejected = (context, cancellationToken) =>
        new ValueTask(context.HttpContext.Response.WriteErrorAsync(
            "Too many payment attempts. Please wait a minute.",
            "rate_limited",
            cancellationToken));
});

builder.Services
    .AddSingleton<OrderStore>()
    .AddSingleton<PaymentGateway>()
    .AddSingleton<IValidator<AddLineRequest>, AddLineRequestValidator>()
    .AddSingleton<OrderService>()
    .AddSingleton<AdminOrderService>();

WebApplication app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// GET api/error-codes: the default catalog, open to anyone.
app.MapErrorCodes();

// GET api/admin/error-codes: the "admin" catalog, for admins only.
app.MapErrorCodes("api/admin/error-codes", "admin").RequireAuthorization(Policies.Admin);

app.MapOrderEndpoints();
app.MapAdminEndpoints();

await app.RunAsync();
