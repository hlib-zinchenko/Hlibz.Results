using System.Text.Json;

using FluentValidation;

using Hlibz.Results.AspNetCore;

using Shop.Admin;
using Shop.Core.Orders;
using Shop.Core.Payments;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Controllers read MVC's JSON options, so that's where the naming policy goes.
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower);

// Default status codes (validation failures stay 400). UseEnvelope makes errors written outside
// the controllers, e.g. by the auth gate, use the same envelope as the actions.
builder.Services.AddResultErrorResponses(options => options.UseEnvelope = true);

builder.Services
    .AddErrorCodeCatalog(typeof(OrdersErrorCodes).Assembly)
    .AddErrorCodeCatalog(
        "admin",
        typeof(OrdersErrorCodes).Assembly,
        typeof(AdminErrorCodes).Assembly);

builder.Services
    .AddSingleton<OrderStore>()
    .AddSingleton<PaymentGateway>()
    .AddSingleton<IValidator<AddLineRequest>, AddLineRequestValidator>()
    .AddSingleton<OrderService>();

WebApplication app = builder.Build();

app.MapControllers();

await app.RunAsync();
