using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class MinimalApiTests
{
    public static TheoryData<string, HttpStatusCode, string> Failures => new()
    {
        { "not-found", HttpStatusCode.NotFound, "Resource was not found." },
        { "unauthorized", HttpStatusCode.Unauthorized, "Authentication is required." },
        { "forbidden", HttpStatusCode.Forbidden, "Access to the resource is forbidden." },
        {
            "conflict",
            HttpStatusCode.Conflict,
            "The request conflicts with the current state of the resource."
        },
        {
            "unavailable",
            HttpStatusCode.ServiceUnavailable,
            "The service is temporarily unavailable. Please retry later."
        },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ToHttpResult_WithValue_Returns200WithValue()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapGet("/order", () => Result<OrderDto>.Success(new(7, "Ann")).ToHttpResult()));

        using HttpResponseMessage response = await client.GetAsync("/order", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, body.GetProperty("order_id").GetInt32());
        Assert.Equal("Ann", body.GetProperty("customer_name").GetString());
    }

    [Fact]
    public async Task ToHttpResult_WithoutValue_ReturnsEmpty200()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapPost("/done", () => Result.Success().ToHttpResult()));

        using HttpResponseMessage response = await client.PostAsync("/done", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync(Ct));
    }

    [Theory]
    [MemberData(nameof(Failures))]
    public async Task ToHttpResult_WithFailureWithoutMessage_SendsStatusAndDefaultMessage(
        string route,
        HttpStatusCode expectedStatus,
        string expectedMessage)
    {
        using HttpClient client = await TestApp.StartAsync(app =>
        {
            app.MapGet("/not-found", () => Result<OrderDto>.NotFound().ToHttpResult());
            app.MapGet("/unauthorized", () => Result.Unauthorized().ToHttpResult());
            app.MapGet("/forbidden", () => Result.Forbidden().ToHttpResult());
            app.MapGet("/conflict", () => Result.Conflict().ToHttpResult());
            app.MapGet("/unavailable", () => Result.Unavailable().ToHttpResult());
        });

        using HttpResponseMessage response = await client.GetAsync($"/{route}", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal(expectedMessage, body.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("code").ValueKind);
        Assert.False(body.TryGetProperty("validation_errors", out _));
    }

    [Fact]
    public async Task ToHttpResult_WithErrorCode_SendsCodeAndItsMessage()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapGet("/", () => Result.Forbidden(TestErrorCodes.OrderClosed).ToHttpResult()));

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("test_1001", body.GetProperty("code").GetString());
        Assert.Equal("The order is closed.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ToHttpResult_WithValidationErrors_CasesEachPathSegmentByJsonPolicy()
    {
        using HttpClient client = await TestApp.StartAsync(app => app.MapGet("/", Invalid));

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement errors = (await response.ReadJsonAsync()).GetProperty("validation_errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Too low.", errors.GetProperty("lines[1].unit_price")[0].GetString());
        Assert.Equal("Required.", errors.GetProperty("customer_name")[0].GetString());
    }

    [Fact]
    public async Task ToHttpResult_WithAsIsKeyNaming_KeepsKeysAsGiven()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapGet("/", Invalid),
            options => options.ValidationKeyNaming = ValidationKeyNaming.AsIs);

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement errors = (await response.ReadJsonAsync()).GetProperty("validation_errors");

        Assert.True(errors.TryGetProperty("Lines[1].UnitPrice", out _));
    }

    [Fact]
    public async Task ToHttpResult_WithInvalidMappedTo422_Sends422ButKeepsErrorAt400()
    {
        using HttpClient client = await TestApp.StartAsync(
            app =>
            {
                app.MapGet("/invalid", Invalid);
                app.MapGet("/error", () => Result.Error(TestErrorCodes.OrderClosed).ToHttpResult());
            },
            options => options.StatusCodes[ResultStatus.Invalid] =
                Microsoft.AspNetCore.Http.StatusCodes.Status422UnprocessableEntity);

        using HttpResponseMessage invalid = await client.GetAsync("/invalid", Ct);
        using HttpResponseMessage error = await client.GetAsync("/error", Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.True((await invalid.ReadJsonAsync()).TryGetProperty("validation_errors", out _));
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
    }

    [Fact]
    public async Task ToEnvelopeHttpResult_WithRemappedStatus_EnvelopeStatusMatchesResponse()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapGet(
                "/",
                () => Result.Error(TestErrorCodes.OrderClosed).ToEnvelopeHttpResult()),
            options => options.StatusCodes[ResultStatus.Error] =
                Microsoft.AspNetCore.Http.StatusCodes.Status422UnprocessableEntity);

        using HttpResponseMessage response = await client.GetAsync("/", Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(422, (await response.ReadJsonAsync()).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task ToHttpResult_WithUnmappedStatus_Throws()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapGet("/", () => Result.Conflict().ToHttpResult()),
            options => options.StatusCodes.Remove(ResultStatus.Conflict));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.GetAsync("/", Ct));
    }

    [Fact]
    public async Task ToHttpResult_WithCustomMessages_UsesThem()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapGet("/", () => Result.NotFound().ToHttpResult()),
            options => options.Messages.NotFound = "Nothing here.");

        using HttpResponseMessage response = await client.GetAsync("/", Ct);

        Assert.Equal(
            "Nothing here.",
            (await response.ReadJsonAsync()).GetProperty("message").GetString());
    }

    [Fact]
    public async Task ToEnvelopeHttpResult_WithValue_WrapsValueWithSuccessFields()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapGet(
                "/",
                () => Result<OrderDto>.Success(new(7, "Ann")).ToEnvelopeHttpResult()));

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(body.GetProperty("is_success").GetBoolean());
        Assert.Equal(200, body.GetProperty("status").GetInt32());
        Assert.Equal("Success.", body.GetProperty("message").GetString());
        Assert.Equal(7, body.GetProperty("data").GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task ToEnvelopeHttpResult_WithFailure_SendsStatusCodeAndEnvelope()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapGet(
                "/",
                () => Result<OrderDto>.Conflict(TestErrorCodes.Duplicate).ToEnvelopeHttpResult()));

        using HttpResponseMessage response = await client.GetAsync("/", Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.False(body.GetProperty("is_success").GetBoolean());
        Assert.Equal(409, body.GetProperty("status").GetInt32());
        Assert.Equal("test_1002", body.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
    }

    private static Microsoft.AspNetCore.Http.IResult Invalid() =>
        Result<OrderDto>.Invalid(new Dictionary<string, string[]>
        {
            ["Lines[1].UnitPrice"] = ["Too low."],
            ["CustomerName"] = ["Required."],
        }).ToHttpResult();
}
