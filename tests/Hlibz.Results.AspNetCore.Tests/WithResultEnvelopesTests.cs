using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class WithResultEnvelopesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithResultEnvelopes_WithSuccess_SendsEnvelopeWithData()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapGet("/", () => Result<OrderDto>.Success(new(7, "Ann"))).WithResultEnvelopes());

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("is_success").GetBoolean());
        Assert.Equal(7, body.GetProperty("data").GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task WithResultEnvelopes_WithFailure_SendsEnvelopeWithItsStatus()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapGet("/", () => Result<OrderDto>.NotFound("Order 7 was not found."))
                .WithResultEnvelopes());

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.False(body.GetProperty("is_success").GetBoolean());
        Assert.Equal("Order 7 was not found.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task WithResultEnvelopes_WithInvalid_SendsCasedValidationErrors()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapPost(
                    "/",
                    () => Result<OrderDto>.Invalid(
                        new Dictionary<string, string[]> { ["CustomerName"] = ["Required."] }))
                .WithResultEnvelopes());

        using HttpResponseMessage response = await client.PostAsync("/", null, Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "Required.",
            body.GetProperty("validation_errors").GetProperty("customer_name")[0].GetString());
    }

    [Fact]
    public async Task WithResultEnvelopes_WithResultWithoutValue_SendsEnvelopeWithoutData()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapDelete("/", () => Result.Success()).WithResultEnvelopes());

        using HttpResponseMessage response = await client.DeleteAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("is_success").GetBoolean());
        Assert.False(body.TryGetProperty("data", out _));
    }

    [Fact]
    public async Task WithResultEnvelopes_WithAsyncHandler_SendsEnvelope()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapGet("/", async () =>
                {
                    await Task.Yield();
                    return Result<OrderDto>.Success(new(7, "Ann"));
                })
                .WithResultEnvelopes());

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, body.GetProperty("data").GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task WithResultEnvelopes_OnGroup_LeavesOtherHandlersAlone()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
        {
            RouteGroupBuilder group = app.MapGroup("/orders").WithResultEnvelopes();
            group.MapGet("/result", () => Result<OrderDto>.Success(new(7, "Ann")));
            group.MapGet("/plain", () => new OrderDto(8, "Bob"));
        });

        using HttpResponseMessage enveloped = await client.GetAsync("/orders/result", Ct);
        using HttpResponseMessage plain = await client.GetAsync("/orders/plain", Ct);

        Assert.True((await enveloped.ReadJsonAsync()).GetProperty("is_success").GetBoolean());
        Assert.Equal(8, (await plain.ReadJsonAsync()).GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task WithResultEnvelopes_WithShortCircuitingFilter_SendsItsResponse()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapGet("/", () => Result<OrderDto>.Success(new(7, "Ann")))
                .WithResultEnvelopes()
                .AddEndpointFilter((_, _) => ValueTask.FromResult<object?>(TypedResults.Conflict())));

        using HttpResponseMessage response = await client.GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task WithResultEnvelopes_WithResultOfT_Documents200WithEnvelopeOfT()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(app =>
                app.MapGet("/", () => Result<OrderDto>.NotFound()).WithResultEnvelopes()));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(typeof(ResultEnvelope<OrderDto>), response.Type);
        Assert.Equal(["application/json"], response.ContentTypes);
    }

    [Fact]
    public async Task WithResultEnvelopes_WithResult_Documents200WithEnvelope()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(app =>
                app.MapDelete("/", () => Result.Success()).WithResultEnvelopes()));

        Assert.Equal(typeof(ResultEnvelope), response.Type);
    }

    [Fact]
    public async Task WithResultEnvelopes_WithCreateEnvelope_Documents200WithoutType()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(
                app => app.MapGet("/", () => Result<OrderDto>.NotFound()).WithResultEnvelopes(),
                options => options.CreateEnvelope = content => new { ok = content.IsSuccess }));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Null(response.Type);
    }

    [Fact]
    public async Task WithResultEnvelopes_WithOtherHandler_KeepsItsDocumentation()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(app =>
                app.MapGet("/", () => new OrderDto(8, "Bob")).WithResultEnvelopes()));

        Assert.Equal(typeof(OrderDto), response.Type);
    }
}
