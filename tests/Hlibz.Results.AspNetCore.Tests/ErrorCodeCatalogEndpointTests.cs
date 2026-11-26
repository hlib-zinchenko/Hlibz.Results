using System.Net;
using System.Reflection;
using System.Text.Json;

using Hlibz.Results.AspNetCore.Tests.Admin;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class ErrorCodeCatalogEndpointTests
{
    private static readonly Assembly AdminAssembly = typeof(AdminOnly).Assembly;

    private static readonly string[] UserCodes =
    [
        "forbidden", "test_1001", "test_1002", "test_1003", "test_1004", "test_1005", "test_1006",
        "unauthorized", "unavailable", "validation_failed",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MapErrorCodes_WithNamedCatalog_PublishesEachCatalogSeparately()
    {
        using HttpClient client = await TestApp.StartAsync(
            app =>
            {
                app.MapErrorCodes();
                app.MapErrorCodes("api/admin/error-codes", "admin");
            },
            services: services => services.AddErrorCodeCatalog(
                "admin",
                typeof(TestErrors).Assembly,
                AdminAssembly));

        using HttpResponseMessage user = await client.GetAsync("/api/error-codes", Ct);
        using HttpResponseMessage admin = await client.GetAsync("/api/admin/error-codes", Ct);

        Assert.Equal(UserCodes, Codes(await user.ReadJsonAsync()));
        Assert.Equal(["admin_1001", .. UserCodes], Codes(await admin.ReadJsonAsync()));
    }

    [Fact]
    public async Task MapErrorCodes_ForAnErrorWithData_PublishesItsStatusAndDetailsSchema()
    {
        using HttpClient client = await TestApp.StartAsync(app => app.MapErrorCodes());

        using HttpResponseMessage response = await client.GetAsync("/api/error-codes", Ct);
        JsonElement[] entries = [.. (await response.ReadJsonAsync()).EnumerateArray()];
        JsonElement notFound = entries.Single(entry => entry.GetProperty("code").GetString() == "test_1003");
        JsonElement closed = entries.Single(entry => entry.GetProperty("code").GetString() == "test_1001");

        Assert.Equal(404, notFound.GetProperty("status").GetInt32());
        Assert.Equal("Order 7 was not found.", notFound.GetProperty("message").GetString());
        Assert.True(notFound.GetProperty("details").GetProperty("properties").TryGetProperty("order_id", out _));
        Assert.False(closed.TryGetProperty("details", out _));
    }

    [Fact]
    public async Task MapErrorCodes_WithUnregisteredName_FailsAtStartup()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestApp.StartAsync(app => app.MapErrorCodes("api/x/error-codes", "missing")));

        Assert.Contains("'missing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NamedCatalog_InController_ResolvesAsKeyedService()
    {
        using HttpClient client = await TestApp.StartAsync(
            _ => { },
            services: services => services.AddErrorCodeCatalog("admin", AdminAssembly));

        using HttpResponseMessage response = await client.GetAsync("/mvc/admin-error-codes", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["admin_1001", "forbidden", "unauthorized", "unavailable", "validation_failed"],
            (await response.ReadJsonAsync()).EnumerateArray().Select(code => code.GetString()));
    }

    private static string?[] Codes(JsonElement body) =>
        [.. body.EnumerateArray().Select(item => item.GetProperty("code").GetString())];
}

[ApiController]
[Route("mvc")]
public sealed class AdminErrorCodesController : ControllerBase
{
    [HttpGet("admin-error-codes")]
    public IEnumerable<string> Get([FromKeyedServices("admin")] IErrorCodeCatalog catalog) =>
        catalog.All.Select(error => error.Code);
}
