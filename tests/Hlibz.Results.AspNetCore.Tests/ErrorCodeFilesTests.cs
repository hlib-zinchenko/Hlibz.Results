using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

using Hlibz.Results.AspNetCore.Internal;
using Hlibz.Results.AspNetCore.Tool;

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class ErrorCodeFilesTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"error-codes-{Guid.NewGuid():N}");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Write_WithDefaultAndNamedCatalogs_WritesWhatEachEndpointServes()
    {
        IServiceProvider? services = null;
        using HttpClient client = await TestApp.StartAsync(
            app =>
            {
                services = app.Services;
                app.MapErrorCodes();
                app.MapErrorCodes("api/admin/error-codes", "admin");
            },
            services: collection => collection
                .AddErrorCodeCatalog("admin", typeof(TestErrorCodes).Assembly)
                .ConfigureHttpJsonOptions(json =>
                    json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseUpper));

        // Through the tool's call by name, which must find the writer.
        IReadOnlyList<string> files = ErrorCodeFilesWriter.Write(services!, _directory);

        Assert.Equal(
            [Path.Combine(_directory, "error-codes.json"), Path.Combine(_directory, "error-codes.admin.json")],
            files);
        Assert.True(JsonNode.DeepEquals(
            await ServedAsync(client, "/api/error-codes"),
            JsonNode.Parse(await File.ReadAllTextAsync(files[0], Ct))));
        Assert.True(JsonNode.DeepEquals(
            await ServedAsync(client, "/api/admin/error-codes"),
            JsonNode.Parse(await File.ReadAllTextAsync(files[1], Ct))));
        Assert.Contains("\"CODE\": \"test_1001\"", await File.ReadAllTextAsync(files[0], Ct), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_WithUnchangedCodes_LeavesFileUntouched()
    {
        IServiceProvider services = Services(collection => collection.AddErrorCodeCatalog(typeof(TestErrorCodes).Assembly));
        string file = Assert.Single(ErrorCodeFiles.Write(services, _directory));
        DateTime past = new(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(file, past);

        _ = ErrorCodeFiles.Write(services, _directory);

        Assert.Equal(past, File.GetLastWriteTimeUtc(file));
    }

    [Fact]
    public void Write_WithChangedCodes_RewritesFile()
    {
        IServiceProvider services = Services(collection => collection.AddErrorCodeCatalog(typeof(TestErrorCodes).Assembly));
        Directory.CreateDirectory(_directory);
        string file = Path.Combine(_directory, ErrorCodeFiles.DefaultFileName);
        File.WriteAllText(file, "[]");

        _ = ErrorCodeFiles.Write(services, _directory);

        Assert.Contains("test_1001", File.ReadAllText(file), StringComparison.Ordinal);
    }

    [Fact]
    public void Write_WithoutCatalog_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ErrorCodeFiles.Write(Services(_ => { }), _directory));

        Assert.Contains("AddErrorCodeCatalog", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Write_WithCatalogNameInvalidInFileName_Throws()
    {
        IServiceProvider services = Services(collection => collection.AddErrorCodeCatalog("admin/v2", typeof(TestErrorCodes).Assembly));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ErrorCodeFiles.Write(services, _directory));

        Assert.Contains("'admin/v2'", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Capture_WithWebApplicationEntryPoint_ReturnsItsServicesWithoutRunningPastBuild()
    {
        IServiceProvider services = AppServices.Capture(EntryPoint(nameof(WebAppMain)), [], TimeSpan.FromMinutes(1));

        Assert.NotNull(services.GetService<IErrorCodeCatalog>());
    }

    [Fact]
    public void Capture_WithEnvironmentArgument_BuildsTheHostInThatEnvironment()
    {
        IServiceProvider services = AppServices.Capture(
            EntryPoint(nameof(WebAppMain)),
            ["--environment", "Staging"],
            TimeSpan.FromMinutes(1));

        Assert.Equal("Staging", services.GetRequiredService<IHostEnvironment>().EnvironmentName);
    }

    [Fact]
    public void Capture_WithHostBuiltAfterAwait_ReturnsItsServices()
    {
        IServiceProvider services = AppServices.Capture(EntryPoint(nameof(AwaitingMain)), [], TimeSpan.FromMinutes(1));

        Assert.NotNull(services.GetService<IErrorCodeCatalog>());
    }

    [Fact]
    public void Capture_WithEntryPointThrowingBeforeBuild_ThrowsItsException()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AppServices.Capture(EntryPoint(nameof(ThrowingMain)), [], TimeSpan.FromMinutes(1)));

        Assert.Equal("Startup failed.", exception.Message);
    }

    [Fact]
    public void Capture_WithEntryPointBuildingNoHost_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => AppServices.Capture(EntryPoint(nameof(NoHostMain)), [], TimeSpan.FromMinutes(1)));

        Assert.Contains("without building a host", exception.Message, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static async Task<JsonNode?> ServedAsync(HttpClient client, string path) =>
        JsonNode.Parse(await client.GetStringAsync(new Uri(path, UriKind.Relative), Ct));

    private static ServiceProvider Services(Action<IServiceCollection> configure)
    {
        ServiceCollection services = new();
        services.AddOptions();
        configure(services);
        return services.BuildServiceProvider();
    }

    private static MethodInfo EntryPoint(string name) =>
        typeof(ErrorCodeFilesTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!;

    private static void WebAppMain(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(
            ["--hostBuilder:reloadConfigOnChange=false", .. args]);
        builder.Services.AddErrorCodeCatalog(typeof(TestErrorCodes).Assembly);
        _ = builder.Build();
        throw new InvalidOperationException("Ran past Build().");
    }

    private static void AwaitingMain()
    {
        Task build = Task.Run(
            async () =>
            {
                await Task.Yield();
                WebAppMain([]);
            },
            CancellationToken.None);
        build.GetAwaiter().GetResult();
    }

    private static void ThrowingMain() => throw new InvalidOperationException("Startup failed.");

    private static void NoHostMain()
    {
        // Returns without building a host.
    }
}
