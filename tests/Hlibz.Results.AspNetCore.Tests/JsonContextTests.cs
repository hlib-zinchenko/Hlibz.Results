using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Hlibz.Results.AspNetCore.Internal;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Hlibz.Results.AspNetCore.Tests;

/// <summary>
/// The source-generated metadata behind Native AOT. The other tests serialize through
/// reflection, which comes first in a JIT app's resolver chain; these use the context alone.
/// </summary>
public sealed class JsonContextTests
{
    private static readonly JsonSerializerOptions SnakeCaseContextOnly = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        TypeInfoResolver = ResultsJsonContext.Default,
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void ResultsJsonContext_WithNamingPolicy_NamesPropertiesByIt()
    {
        ErrorResponse error = new()
        {
            Message = "Check the fields.",
            Code = "order_invalid",
            ValidationErrors = new Dictionary<string, string[]> { ["customer_name"] = ["Required."] },
        };

        string json = JsonSerializer.Serialize(error, SnakeCaseContextOnly);

        Assert.Equal(
            """{"message":"Check the fields.","code":"order_invalid","validation_errors":{"customer_name":["Required."]}}""",
            json);
    }

    [Fact]
    public async Task AddResultErrorResponses_WithoutReflection_WritesErrorsAndEnvelopes()
    {
        using HttpClient client = await TestApp.StartAsync(
            app =>
            {
                app.MapGet("/error", () => Result.NotFound("Order 7 was not found.").ToHttpResult());
                app.MapGet("/envelope", () => Result.Conflict("Already sent.").ToEnvelopeHttpResult());
                app.MapGet("/written", (HttpContext context) =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return context.Response.WriteErrorAsync(
                        new ErrorResponse { Message = "No.", Code = null },
                        context.RequestAborted);
                });
            },
            services: services => services.PostConfigure<HttpJsonOptions>(json =>
                KeepOnly<ResultsJsonContext>(json.SerializerOptions.TypeInfoResolverChain)));

        using HttpResponseMessage error = await client.GetAsync("/error", Ct);
        using HttpResponseMessage envelope = await client.GetAsync("/envelope", Ct);
        using HttpResponseMessage written = await client.GetAsync("/written", Ct);

        Assert.Equal(HttpStatusCode.NotFound, error.StatusCode);
        Assert.Equal("Order 7 was not found.", (await error.ReadJsonAsync()).GetProperty("message").GetString());
        Assert.Equal(HttpStatusCode.Conflict, envelope.StatusCode);
        Assert.False((await envelope.ReadJsonAsync()).GetProperty("is_success").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, written.StatusCode);
        Assert.Equal("No.", (await written.ReadJsonAsync()).GetProperty("message").GetString());
    }

    [Fact]
    public void AddResultErrorResponses_AddsContextAfterTheAppsResolvers()
    {
        ServiceCollection services = new();
        services.AddResultErrorResponses();
        using ServiceProvider provider = services.BuildServiceProvider();

        IList<IJsonTypeInfoResolver> chain = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<HttpJsonOptions>>()
            .Value.SerializerOptions.TypeInfoResolverChain;
        IList<IJsonTypeInfoResolver> mvcChain = provider
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<Microsoft.AspNetCore.Mvc.JsonOptions>>()
            .Value.JsonSerializerOptions.TypeInfoResolverChain;

        Assert.Same(ResultsJsonContext.Default, chain[^1]);
        Assert.Same(ResultsJsonContext.Default, mvcChain[^1]);
    }

    private static void KeepOnly<TResolver>(IList<IJsonTypeInfoResolver> chain)
    {
        for (int index = chain.Count - 1; index >= 0; index--)
        {
            if (chain[index] is not TResolver)
            {
                chain.RemoveAt(index);
            }
        }
    }
}
