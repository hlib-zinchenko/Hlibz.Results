using System.Text.Encodings.Web;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Hlibz.Results.Mcp.Tests;

public sealed class ToolErrorTests
{
    private static readonly JsonSerializerOptions Readable = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly Guid CityId = Guid.Parse("6f1c2a5e-0b7d-4e7f-9a51-2f0b5c3d8e11");

    [Fact]
    public async Task ToolsList_ToolWithDeclaredErrors_ListsThemInMetaWithGuidance()
    {
        await using McpTestServer server = await McpTestServer.StartAsync();

        JsonObject? meta = await server.MetaOfAsync("delete_city");

        Assert.Equal(
            """[{"code":"city_not_found","description":"The city doesn't exist.","guidance":"No city has id {cityId}. Call list_cities to find its id."},"""
            + """{"code":"city_in_use","description":"Users live in the city.","guidance":"{userCount} users live in city {cityId}. Ask the user whether to move them first."},"""
            + """{"code":"validation_failed","description":"Validation failed. Please check the validation errors.","guidance":"Fix these arguments and call the tool again: {errors}"}]""",
            meta?[ToolErrorOptions.MetaKey]?.ToJsonString(Readable));
    }

    [Fact]
    public async Task ToolsList_ToolWithoutErrors_HasNoErrorsInMeta()
    {
        await using McpTestServer server = await McpTestServer.StartAsync();

        JsonObject? meta = await server.MetaOfAsync("ping");

        Assert.Null(meta?[ToolErrorOptions.MetaKey]);
    }

    [Fact]
    public async Task ToolCall_ThatFails_SaysWhatToDoWithTheDetailsFilledIn()
    {
        await using McpTestServer server = await McpTestServer.StartAsync();

        CallToolResult result = await server.CallAsync("delete_city", new() { ["cityId"] = CityId, ["outcome"] = "used" });

        Assert.True(result.IsError);
        Assert.Equal(
            $"city_in_use: 3 users live in city {CityId}. Ask the user whether to move them first.",
            Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.Equal(
            $$$"""{"code":"city_in_use","details":{"cityId":"{{{CityId}}}","userCount":3}}""",
            JsonSerializer.Serialize(result.StructuredContent));
    }

    [Fact]
    public async Task ToolCall_ThatFailsValidation_KeysTheMessagesByTheNamingPolicy()
    {
        await using McpTestServer server = await McpTestServer.StartAsync();

        CallToolResult result = await server.CallAsync("delete_city", new() { ["cityId"] = CityId, ["outcome"] = "invalid" });

        Assert.Equal(
            "validation_failed: Fix these arguments and call the tool again: items[0].unitPrice: Must be positive.; name: Required. Too short.",
            Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.Equal(
            """{"code":"validation_failed","details":{"errors":{"items[0].unitPrice":["Must be positive."],"name":["Required.","Too short."]}}}""",
            JsonSerializer.Serialize(result.StructuredContent));
    }

    [Fact]
    public async Task ToolCall_ErrorWithoutData_HasOnlyItsCode()
    {
        await using McpTestServer server = await McpTestServer.StartAsync();

        CallToolResult result = await server.CallAsync("close_city");

        Assert.True(result.IsError);
        Assert.Equal("""{"code":"city_closed"}""", JsonSerializer.Serialize(result.StructuredContent));
    }

    [Fact]
    public async Task ToolCall_ThatSucceeds_ReturnsTheValue()
    {
        await using McpTestServer server = await McpTestServer.StartAsync();

        CallToolResult result = await server.CallAsync("delete_city", new() { ["cityId"] = CityId, ["outcome"] = "ok" });

        Assert.NotEqual(true, result.IsError);
        Assert.Equal("deleted", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
    }

    [Fact]
    public async Task ToolCall_WithoutRequiredGuidance_SaysTheErrorsMessage()
    {
        await using McpTestServer server = await McpTestServer.StartAsync(mcp => mcp
            .WithToolsFromAssembly(typeof(CityTools).Assembly)
            .WithToolErrors(errors => errors.RequireGuidance = false));

        CallToolResult result = await server.CallAsync("delete_city", new() { ["cityId"] = CityId, ["outcome"] = "missing" });

        Assert.Equal("city_not_found: The city doesn't exist.", Assert.IsType<TextContentBlock>(Assert.Single(result.Content)).Text);
        Assert.Null((await server.MetaOfAsync("delete_city"))?[ToolErrorOptions.MetaKey]?[0]?["guidance"]);
    }

    [Fact]
    public async Task Start_WithAnErrorWithoutGuidance_FailsNamingTheToolAndTheError()
    {
        using IHost host = McpTestServer.Build(mcp => mcp
            .WithToolsFromAssembly(typeof(CityTools).Assembly)
            .WithToolErrors(errors => errors.Guide<CityNotFound>("No city has id {cityId}.")));

        OptionsValidationException failure = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Contains(
            "Tool 'delete_city' can fail with CityInUse (city_in_use), which has no guidance",
            failure.Message,
            StringComparison.Ordinal);
        Assert.Contains("Tool 'close_city' can fail with CityClosed (city_closed)", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Start_WithGuidanceNamingADetailTheErrorLacks_Fails()
    {
        using IHost host = McpTestServer.Build(mcp => mcp
            .WithToolsFromAssembly(typeof(CityTools).Assembly)
            .WithToolErrors(McpTestServer.Guidance)
            .WithToolErrors(errors => errors.Guide<CityInUse>("{users} users live in city {cityId}.")));

        OptionsValidationException failure = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            "CityInUse's guidance names {users}, which isn't one of its details ({cityId}, {userCount}).",
            Assert.Single(failure.Failures));
    }

    [Fact]
    public async Task ToolsList_WithAnErrorSource_ListsItsErrorsAfterTheDeclaredOnesOnce()
    {
        await using McpTestServer server = await McpTestServer.StartAsync(mcp =>
        {
            mcp.Services.AddSingleton<IToolErrorSource, ClosedCities>();
            mcp.WithToolErrors(McpTestServer.Guidance).WithToolsFromAssembly(typeof(CityTools).Assembly);
        });

        JsonObject? meta = await server.MetaOfAsync("delete_city");

        Assert.Equal(
            ["city_not_found", "city_in_use", "validation_failed", "city_closed"],
            meta![ToolErrorOptions.MetaKey]!.AsArray().Select(error => (string?)error!["code"]));
    }

    [Fact]
    public async Task Start_WithASourceListingANonError_Fails()
    {
        using IHost host = McpTestServer.Build(mcp =>
        {
            mcp.Services.AddSingleton<IToolErrorSource, NotAnError>();
            mcp.WithToolsFromAssembly(typeof(CityTools).Assembly).WithToolErrors(McpTestServer.Guidance);
        });

        OptionsValidationException failure = await Assert.ThrowsAsync<OptionsValidationException>(
            () => host.StartAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Tool 'ping' lists System.String, which isn't an error type.", Assert.Single(failure.Failures));
    }

    [Fact]
    public void ValueOrThrow_OfAFailure_ThrowsAnMcpExceptionWithTheCodeAndMessage()
    {
        Result<string> result = new CityNotFound(CityId);

        McpException failure = Assert.ThrowsAny<McpException>(() => result.ValueOrThrow());

        Assert.Equal("city_not_found: The city doesn't exist.", failure.Message);
        Assert.IsType<CityNotFound>(Assert.IsType<ToolErrorException>(failure).Error);
    }

    // Every city tool can also fail as closed; it lists one the tool declares too, listed once.
    private sealed class ClosedCities : IToolErrorSource
    {
        public IEnumerable<Type> ErrorsOf(McpServerTool tool) =>
            tool.ProtocolTool.Name.EndsWith("_city", StringComparison.Ordinal) ? [typeof(CityNotFound), typeof(CityClosed)] : [];
    }

    private sealed class NotAnError : IToolErrorSource
    {
        public IEnumerable<Type> ErrorsOf(McpServerTool tool) => tool.ProtocolTool.Name == "ping" ? [typeof(string)] : [];
    }
}
