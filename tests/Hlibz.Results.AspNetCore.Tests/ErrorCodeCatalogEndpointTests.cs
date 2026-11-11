using System.Net;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class ErrorCodeCatalogEndpointTests
{
    private static readonly Assembly AdminAssembly = BuildAssembly("admin_1001");

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
                typeof(TestErrorCodes).Assembly,
                AdminAssembly));

        using HttpResponseMessage user = await client.GetAsync("/api/error-codes", Ct);
        using HttpResponseMessage admin = await client.GetAsync("/api/admin/error-codes", Ct);

        Assert.Equal(["test_1001", "test_1002"], Codes(await user.ReadJsonAsync()));
        Assert.Equal(
            ["admin_1001", "test_1001", "test_1002"],
            Codes(await admin.ReadJsonAsync()));
    }

    [Fact]
    public async Task MapErrorCodes_WithUnregisteredName_FailsAtStartup()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestApp.StartAsync(app => app.MapErrorCodes("api/x/error-codes", "missing")));

        Assert.Contains("'missing'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AddErrorCodeCatalog_WithDuplicateCodesInNamedCatalog_FailsAtRegistration()
    {
        ServiceCollection services = new();

        Assert.Throws<InvalidOperationException>(
            () => services.AddErrorCodeCatalog(
                "dup",
                typeof(TestErrorCodes).Assembly,
                BuildAssembly("test_1001")));
    }

    [Fact]
    public async Task NamedCatalog_InController_ResolvesAsKeyedService()
    {
        using HttpClient client = await TestApp.StartAsync(
            _ => { },
            services: services => services.AddErrorCodeCatalog("admin", AdminAssembly));

        using HttpResponseMessage response = await client.GetAsync("/mvc/admin-error-codes", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["admin_1001"], Codes(await response.ReadJsonAsync()));
    }

    private static string?[] Codes(JsonElement body) =>
        [.. body.EnumerateArray().Select(item => item.GetProperty("code").GetString())];

    private static AssemblyBuilder BuildAssembly(string code)
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName($"Codes_{code}_{Guid.NewGuid():N}"),
            AssemblyBuilderAccess.Run);
        TypeBuilder type = assembly
            .DefineDynamicModule("Codes")
            .DefineType("Codes", TypeAttributes.Public | TypeAttributes.Class);
        type.DefineField("Code", typeof(ErrorCode), FieldAttributes.Public | FieldAttributes.Static);
        Type built = type.CreateType();
        built.GetField("Code")!.SetValue(null, new ErrorCode(code, "Generated."));
        return assembly;
    }
}

[ApiController]
[Route("mvc")]
public sealed class AdminErrorCodesController : ControllerBase
{
    [HttpGet("admin-error-codes")]
    public IReadOnlyList<ErrorCode> Get([FromKeyedServices("admin")] IErrorCodeCatalog catalog) =>
        catalog.All;
}
