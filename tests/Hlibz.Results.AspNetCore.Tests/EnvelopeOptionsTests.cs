using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class EnvelopeOptionsTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateEnvelope_WithSuccess_WrapsValueInCustomShape()
    {
        using HttpClient client = await StartAsync(
            app => app.MapGet(
                "/",
                () => Result<OrderDto>.Success(new(7, "Ann")).ToEnvelopeHttpResult()),
            Custom);

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("ok").GetBoolean());
        Assert.Equal("Success", body.GetProperty("outcome").GetString());
        Assert.Equal(7, body.GetProperty("payload").GetProperty("order_id").GetInt32());
        Assert.False(body.TryGetProperty("is_success", out _));
    }

    [Fact]
    public async Task CreateEnvelope_WithFailure_CarriesCodeMessageAndStatus()
    {
        using HttpClient client = await StartAsync(
            app => app.MapGet(
                "/",
                () => Result<OrderDto>.Conflict(TestErrorCodes.Duplicate).ToEnvelopeHttpResult()),
            Custom);

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(409, body.GetProperty("http").GetInt32());
        Assert.Equal("Conflict", body.GetProperty("outcome").GetString());
        Assert.Equal("test_1002", body.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task CreateEnvelope_InController_UsesCustomShape()
    {
        using HttpClient client = await StartAsync(_ => { }, Custom);

        using HttpResponseMessage response = await client.GetAsync("/mvc/envelope", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal("Ann", body.GetProperty("payload").GetProperty("customer_name").GetString());
    }

    [Fact]
    public async Task CreateEnvelope_FromAuthGate_HasNoResultStatus()
    {
        using HttpClient client = await StartProtectedAsync(options =>
        {
            Custom(options);
            options.UseEnvelope = true;
        });

        using HttpResponseMessage response = await client.GetAsync("/admin", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(401, body.GetProperty("http").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("outcome").ValueKind);
    }

    [Fact]
    public async Task CreateEnvelope_ReturningNull_Throws()
    {
        using HttpClient client = await StartAsync(
            app => app.MapGet("/", () => Result.Success().ToEnvelopeHttpResult()),
            options => options.CreateEnvelope = _ => null!);

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/", Ct));
    }

    [Fact]
    public async Task EnvelopeAlwaysOk_WithFailure_Sends200WithRealStatusInEnvelope()
    {
        using HttpClient client = await StartAsync(
            app => app.MapGet("/", () => Result<OrderDto>.NotFound().ToEnvelopeHttpResult()),
            options => options.EnvelopeAlwaysOk = true);

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.GetProperty("is_success").GetBoolean());
        Assert.Equal(404, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task EnvelopeAlwaysOk_InController_Sends200()
    {
        using HttpClient client = await StartAsync(
            _ => { },
            options => options.EnvelopeAlwaysOk = true);

        using HttpResponseMessage response = await client.GetAsync("/mvc/envelope-conflict", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(409, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task EnvelopeAlwaysOk_FromAuthGateWithEnvelope_Sends200()
    {
        using HttpClient client = await StartProtectedAsync(options =>
        {
            options.UseEnvelope = true;
            options.EnvelopeAlwaysOk = true;
        });

        using HttpResponseMessage response = await client.GetAsync("/admin", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(401, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task EnvelopeAlwaysOk_WithPlainErrorResponse_KeepsStatusCode()
    {
        using HttpClient client = await StartAsync(
            app => app.MapGet("/", () => Result<OrderDto>.NotFound().ToHttpResult()),
            options => options.EnvelopeAlwaysOk = true);

        using HttpResponseMessage response = await client.GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static void Custom(ResultHttpOptions options) =>
        options.CreateEnvelope = content => new
        {
            Ok = content.IsSuccess,
            Http = content.StatusCode,
            Outcome = content.ResultStatus?.ToString(),
            Error = content.IsSuccess ? null : new { content.Code, content.Message },
            Payload = content.Data,
        };

    private static Task<HttpClient> StartAsync(
        Action<WebApplication> map,
        Action<ResultHttpOptions> configure) =>
        TestApp.StartAsync(map, configure);

    private static Task<HttpClient> StartProtectedAsync(Action<ResultHttpOptions> configure) =>
        TestApp.StartAsync(
            app => app
                .MapGet("/admin", () => Result.Success().ToHttpResult())
                .RequireAuthorization(TestApp.AdminPolicy),
            configure);
}
