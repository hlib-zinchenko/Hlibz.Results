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
        Assert.Equal("Resource was not found.", body.GetProperty("message").GetString());
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

    private static async Task<HttpResponseMessage> GetAsync(string path)
    {
        HttpClient client = await TestApp.StartAsync(_ => { });
        return await client.GetAsync(path, Ct);
    }
}
