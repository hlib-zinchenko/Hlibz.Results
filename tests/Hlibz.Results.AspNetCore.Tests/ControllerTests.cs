using System.Net;
using System.Text.Json;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class ControllerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ToActionResult_WithValue_Returns200WithValue()
    {
        using HttpResponseMessage response = await GetAsync("/mvc/order");
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, body.GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task ToActionResult_WithoutValue_ReturnsEmpty200()
    {
        using HttpClient client = await TestApp.StartAsync(_ => { });

        using HttpResponseMessage response = await client.PostAsync("/mvc/ok", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task ToActionResult_WithNotFound_SendsErrorResponse()
    {
        using HttpResponseMessage response = await GetAsync("/mvc/missing");
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Order 7 was not found.", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ToActionResult_WithValidationErrors_CasesKeysByMvcJsonPolicy()
    {
        using HttpResponseMessage response = await GetAsync("/mvc/invalid");
        JsonElement errors = (await response.ReadJsonAsync()).GetProperty("validation_errors");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(errors.TryGetProperty("lines[1].unit_price", out _));
    }

    [Fact]
    public async Task ToActionResult_WithInvalidMappedTo422_Sends422()
    {
        using HttpClient client = await TestApp.StartAsync(
            _ => { },
            options => options.StatusCodes[ResultStatus.Invalid] = 422);

        using HttpResponseMessage response = await client.GetAsync("/mvc/invalid-422", Ct);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ToEnvelopeActionResult_WithValue_WrapsValue()
    {
        using HttpResponseMessage response = await GetAsync("/mvc/envelope");
        JsonElement body = await response.ReadJsonAsync();

        Assert.True(body.GetProperty("is_success").GetBoolean());
        Assert.Equal("Ann", body.GetProperty("data").GetProperty("customer_name").GetString());
    }

    [Fact]
    public async Task ToEnvelopeActionResult_WithConflict_SendsEnvelopeWith409()
    {
        using HttpResponseMessage response = await GetAsync("/mvc/envelope-conflict");
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(409, body.GetProperty("status").GetInt32());
        Assert.Equal("test_1002", body.GetProperty("code").GetString());
    }

    [Fact]
    public async Task ToActionResult_WithActionAttribute_SendsItsCode()
    {
        using HttpClient client = await TestApp.StartAsync(_ => { });

        using HttpResponseMessage response = await client.PostAsync("/mvc/accepted", null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(7, (await response.ReadJsonAsync()).GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task ToActionResult_WithControllerAttribute_AppliesUnlessTheActionSetsItsOwn()
    {
        using HttpClient client = await TestApp.StartAsync(_ => { });

        using HttpResponseMessage controller = await client.PostAsync("/mvc/imports", null, Ct);
        using HttpResponseMessage action = await client.PostAsync("/mvc/imports/now", null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, controller.StatusCode);
        Assert.Equal(HttpStatusCode.Created, action.StatusCode);
    }

    [Fact]
    public async Task ToActionResult_WithDeleteDefaultOf204_Sends204()
    {
        using HttpClient client = await TestApp.StartAsync(
            _ => { },
            options => options.SuccessStatusCodes["DELETE"] = 204);

        using HttpResponseMessage response = await client.DeleteAsync("/mvc/order", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task ToCreatedActionResult_WithSuccess_Sends201WithLocation()
    {
        using HttpClient client = await TestApp.StartAsync(_ => { });

        using HttpResponseMessage response = await client.PostAsync("/mvc/created", null, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/mvc/order/7", response.Headers.Location?.OriginalString);
        Assert.Equal(7, (await response.ReadJsonAsync()).GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task ToCreatedEnvelopeActionResult_WithSuccess_Sends201EnvelopeWithLocation()
    {
        using HttpClient client = await TestApp.StartAsync(_ => { });

        using HttpResponseMessage response = await client.PostAsync("/mvc/created-envelope", null, Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/mvc/order/7", response.Headers.Location?.OriginalString);
        Assert.Equal(201, body.GetProperty("status").GetInt32());
    }

    private static async Task<HttpResponseMessage> GetAsync(string path)
    {
        HttpClient client = await TestApp.StartAsync(_ => { });
        return await client.GetAsync(path, Ct);
    }
}
