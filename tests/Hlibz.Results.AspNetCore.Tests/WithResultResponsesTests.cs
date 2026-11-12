using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class WithResultResponsesTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task WithResultResponses_WithSuccess_SendsValue()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapGet("/", () => Result<OrderDto>.Success(new(7, "Ann"))).WithResultResponses());

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, body.GetProperty("order_id").GetInt32());
        Assert.False(body.TryGetProperty("is_success", out _));
    }

    [Fact]
    public async Task WithResultResponses_WithFailure_SendsErrorResponseWithItsStatus()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapGet("/", () => Result<OrderDto>.NotFound("Order 7 was not found."))
                .WithResultResponses());

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Order 7 was not found.", body.GetProperty("message").GetString());
        Assert.False(body.TryGetProperty("is_success", out _));
    }

    [Fact]
    public async Task WithResultResponses_WithInvalid_SendsCasedValidationErrors()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapPost(
                    "/",
                    () => Result<OrderDto>.Invalid(
                        new Dictionary<string, string[]> { ["CustomerName"] = ["Required."] }))
                .WithResultResponses());

        using HttpResponseMessage response = await client.PostAsync("/", null, Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "Required.",
            body.GetProperty("validation_errors").GetProperty("customer_name")[0].GetString());
    }

    [Fact]
    public async Task WithResultResponses_WithResultWithoutValue_SendsEmptyBody()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapDelete("/", () => Result.Success()).WithResultResponses());

        using HttpResponseMessage response = await client.DeleteAsync("/", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task WithResultResponses_WithAsyncHandler_SendsValue()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapGet("/", async () =>
                {
                    await Task.Yield();
                    return Result<OrderDto>.Success(new(7, "Ann"));
                })
                .WithResultResponses());

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, body.GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task WithResultResponses_WithAsyncResultWithoutValue_SendsErrorResponse()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapPost("/", async () =>
                {
                    await Task.Yield();
                    return Result.Conflict("Already sent.");
                })
                .WithResultResponses());

        using HttpResponseMessage response = await client.PostAsync("/", null, Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Already sent.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task WithResultResponses_OnGroup_LeavesOtherHandlersAlone()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
        {
            RouteGroupBuilder group = app.MapGroup("/orders").WithResultResponses();
            group.MapGet("/result", () => Result<OrderDto>.NotFound());
            group.MapGet("/plain", () => new OrderDto(8, "Bob"));
        });

        using HttpResponseMessage result = await client.GetAsync("/orders/result", Ct);
        using HttpResponseMessage plain = await client.GetAsync("/orders/plain", Ct);

        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
        Assert.Equal(8, (await plain.ReadJsonAsync()).GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task WithResultResponses_WithShortCircuitingFilter_SendsItsResponse()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapGet("/", () => Result<OrderDto>.Success(new(7, "Ann")))
                .WithResultResponses()
                .AddEndpointFilter((_, _) => ValueTask.FromResult<object?>(TypedResults.Conflict())));

        using HttpResponseMessage response = await client.GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task WithResultResponses_WithResultOfT_Documents200WithValue()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(app =>
                app.MapGet("/", () => Result<OrderDto>.NotFound()).WithResultResponses()));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Equal(typeof(OrderDto), response.Type);
        Assert.Equal(["application/json"], response.ContentTypes);
    }

    [Fact]
    public async Task WithResultResponses_WithResult_Documents200WithoutBody()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(app =>
                app.MapDelete("/", () => Result.Success()).WithResultResponses()));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.Null(response.Type);
    }

    [Fact]
    public async Task WithResultResponses_WithOtherHandler_KeepsItsDocumentation()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(app =>
                app.MapGet("/", () => new OrderDto(8, "Bob")).WithResultResponses()));

        Assert.Equal(typeof(OrderDto), response.Type);
    }
}
