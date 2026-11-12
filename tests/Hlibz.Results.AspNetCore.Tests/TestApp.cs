using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Hlibz.Results.AspNetCore.Tests;

/// <summary>
/// An in-memory app with snake_case JSON (minimal APIs and MVC alike), a header-driven test
/// authentication scheme and <see cref="ResultsController"/>.
/// </summary>
internal static class TestApp
{
    public const string UserHeader = "X-Test-User";

    public const string AdminPolicy = "Admin";

    public static async Task<HttpClient> StartAsync(
        Action<WebApplication> map,
        Action<ResultHttpOptions>? configure = null,
        bool snakeCase = true,
        Action<IServiceCollection>? services = null)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();

        JsonNamingPolicy? policy = snakeCase ? JsonNamingPolicy.SnakeCaseLower : null;
        builder.Services.ConfigureHttpJsonOptions(
            options => options.SerializerOptions.PropertyNamingPolicy = policy);
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(ResultsController).Assembly)
            .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = policy);

        builder.Services
            .AddAuthentication(TestAuthHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(
                TestAuthHandler.SchemeName,
                _ => { });
        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AdminPolicy, policy => policy.RequireRole("admin"));

        builder.Services.AddResultErrorResponses(configure);
        builder.Services.AddErrorCodeCatalog(typeof(TestErrorCodes).Assembly);
        services?.Invoke(builder.Services);

        WebApplication app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        map(app);

        await app.StartAsync(TestContext.Current.CancellationToken);
        return app.GetTestClient();
    }

    /// <summary>
    /// The responses documented for the app's endpoint at <c>/</c>.
    /// </summary>
    public static async Task<IProducesResponseTypeMetadata[]> ResponsesAsync(
        Action<WebApplication> map,
        Action<ResultHttpOptions>? configure = null)
    {
        IEndpointRouteBuilder? routes = null;
        using HttpClient client = await StartAsync(
            app =>
            {
                routes = app;
                map(app);
            },
            configure);

        Endpoint endpoint = Assert.Single(
            routes!.DataSources.SelectMany(source => source.Endpoints),
            item => item is RouteEndpoint { RoutePattern.RawText: "/" });
        return [.. endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()];
    }

    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        // Not JsonElement.Parse: it only exists from .NET 10, and the tests also run on 8 and 9.
        using JsonDocument document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}

/// <summary>
/// Authenticates a request that sends <see cref="TestApp.UserHeader"/>, with its value as the
/// user's role; anything else is anonymous.
/// </summary>
internal sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(TestApp.UserHeader, out Microsoft.Extensions.Primitives.StringValues role))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        ClaimsIdentity identity = new(
            [new Claim(ClaimTypes.Name, "tester"), new Claim(ClaimTypes.Role, role.ToString())],
            SchemeName);
        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

public sealed record TestErrorCode(string Code, string Message) : ErrorCode($"test_{Code}", Message);

public static class TestErrorCodes
{
    public static readonly ErrorCode OrderClosed = new TestErrorCode("1001", "The order is closed.");

    public static readonly ErrorCode Duplicate = new TestErrorCode("1002", "Already exists.");
}

public sealed record OrderDto(int OrderId, string CustomerName);
