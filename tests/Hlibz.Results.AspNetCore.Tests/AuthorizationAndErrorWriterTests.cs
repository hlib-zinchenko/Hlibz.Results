using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class AuthorizationAndErrorWriterTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AuthorizationGate_WithAnonymousCaller_Sends401WithErrorBody()
    {
        using HttpClient client = await StartWithProtectedEndpointAsync();

        using HttpResponseMessage response = await client.GetAsync("/admin", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Authentication is required.", body.GetProperty("message").GetString());
        Assert.Equal("unauthorized", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task AuthorizationGate_WithCallerMissingRole_Sends403WithErrorBody()
    {
        using HttpClient client = await StartWithProtectedEndpointAsync();
        using HttpRequestMessage request = new(HttpMethod.Get, "/admin");
        request.Headers.Add(TestApp.UserHeader, "member");

        using HttpResponseMessage response = await client.SendAsync(request, Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(
            "Access to the resource is forbidden.",
            body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task AuthorizationGate_WithAllowedCaller_RunsEndpoint()
    {
        using HttpClient client = await StartWithProtectedEndpointAsync();
        using HttpRequestMessage request = new(HttpMethod.Get, "/admin");
        request.Headers.Add(TestApp.UserHeader, "admin");

        using HttpResponseMessage response = await client.SendAsync(request, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AuthorizationGate_WithEnvelope_SendsEnvelope()
    {
        using HttpClient client = await StartWithProtectedEndpointAsync(
            options => options.UseEnvelope = true);

        using HttpResponseMessage response = await client.GetAsync("/admin", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.False(body.GetProperty("is_success").GetBoolean());
        Assert.Equal(401, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task AuthorizationGate_WithCookieRedirect_KeepsRedirectWithoutBody()
    {
        using HttpClient client = await StartWithCookieEndpointAsync();

        using HttpResponseMessage response = await client.GetAsync("/account", Ct);
        string body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location?.PathAndQuery, StringComparison.Ordinal);
        Assert.Empty(body);
    }

    [Fact]
    public async Task AuthorizationGate_WithCookieRedirectAndEnvelopeAlwaysOk_KeepsRedirect()
    {
        using HttpClient client = await StartWithCookieEndpointAsync(options =>
        {
            options.UseEnvelope = true;
            options.EnvelopeAlwaysOk = true;
        });

        using HttpResponseMessage response = await client.GetAsync("/account", Ct);
        string body = await response.Content.ReadAsStringAsync(Ct);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Empty(body);
    }

    [Fact]
    public async Task WriteErrorAsync_FromMiddleware_UsesStatusDefaultMessageAndJsonSettings()
    {
        using HttpClient client = await TestApp.StartAsync(app => app.Use(
            async (HttpContext context, RequestDelegate _) =>
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                await context.Response.WriteErrorAsync(
                    "Slow down.",
                    "rate_limited",
                    context.RequestAborted);
            }));

        using HttpResponseMessage response = await client.GetAsync("/anything", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal("Slow down.", body.GetProperty("message").GetString());
        Assert.Equal("rate_limited", body.GetProperty("code").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task WriteErrorAsync_WithBlankCode_Throws(string code)
    {
        DefaultHttpContext context = new();

        await Assert.ThrowsAsync<ArgumentException>(() => context.Response.WriteErrorAsync("Slow down.", code, Ct));
    }

    [Fact]
    public async Task WriteErrorAsync_WithFailedResult_SetsStatusAndWritesBody()
    {
        using HttpClient client = await TestApp.StartAsync(app => app.Use(
            (HttpContext context, RequestDelegate _) => context.Response.WriteErrorAsync(
                Result.Failure(new Duplicate()),
                context.RequestAborted)));

        using HttpResponseMessage response = await client.GetAsync("/anything", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("test_1002", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task WriteErrorAsync_FromRateLimiter_SendsRateLimited()
    {
        using HttpClient client = await TestApp.StartAsync(
            app =>
            {
                app.UseRateLimiter();
                app.MapGet("/limited", () => "ok");
            },
            services: services => services.AddRateLimiter(limiter =>
            {
                limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
                    _ => RateLimitPartition.GetFixedWindowLimiter(
                        "all",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = 1, Window = TimeSpan.FromHours(1) }));
                limiter.OnRejected = (context, cancellationToken) => new ValueTask(
                    context.HttpContext.Response.WriteErrorAsync(Result.Failure(new RateLimited()), cancellationToken));
            }));

        using HttpResponseMessage first = await client.GetAsync("/limited", Ct);
        using HttpResponseMessage second = await client.GetAsync("/limited", Ct);
        JsonElement body = await second.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
        Assert.Equal("rate_limited", body.GetProperty("code").GetString());
        Assert.Equal("Too many requests. Please retry later.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task WriteErrorAsync_WithRemappedStatus_UsesTheAppsCode()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.Use(
                (HttpContext context, RequestDelegate _) => context.Response.WriteErrorAsync(
                    Result.Failure(new RateLimited()),
                    context.RequestAborted)),
            options => options.StatusCodes[ResultStatus.TooManyRequests] = StatusCodes.Status503ServiceUnavailable);

        using HttpResponseMessage response = await client.GetAsync("/anything", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("rate_limited", (await response.ReadJsonAsync()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task MapErrorCodes_WithCatalog_ListsEveryCodeSorted()
    {
        using HttpClient client = await TestApp.StartAsync(app => app.MapErrorCodes());

        using HttpResponseMessage response = await client.GetAsync("/api/error-codes", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        string?[] codes = [.. body.EnumerateArray().Select(item => item.GetProperty("code").GetString())];
        Assert.Equal([.. codes.Order(StringComparer.Ordinal)], codes);
        Assert.Contains("test_1001", codes);
        Assert.Contains("validation_failed", codes);
    }

    private static Task<HttpClient> StartWithProtectedEndpointAsync(
        Action<ResultHttpOptions>? configure = null) =>
        TestApp.StartAsync(
            app => app
                .MapGet("/admin", () => Result.Success().ToHttpResult())
                .RequireAuthorization(TestApp.AdminPolicy),
            configure);

    /// <summary>
    /// An endpoint behind cookie authentication, whose challenge redirects to a login page.
    /// </summary>
    private static Task<HttpClient> StartWithCookieEndpointAsync(
        Action<ResultHttpOptions>? configure = null) =>
        TestApp.StartAsync(
            app => app
                .MapGet("/account", () => Result.Success().ToHttpResult())
                .RequireAuthorization(
                    new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .Build()),
            configure,
            services: services => services.AddAuthentication().AddCookie());
}
