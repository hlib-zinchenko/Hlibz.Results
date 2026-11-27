using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class ProducesErrorSetTests
{
    [Fact]
    public async Task ProducesErrorSet_OnIResultHandler_DocumentsEachStatusAndEveryError()
    {
        Endpoint endpoint = await TestApp.EndpointAsync(app => app
            .MapGet("/", IResult () => TypedResults.Ok())
            .ProducesErrorSet<GetOrderError>());

        DeclaredErrorsMetadata declared = endpoint.Metadata.GetRequiredMetadata<DeclaredErrorsMetadata>();

        Assert.Equal(
            [(404, typeof(ErrorResponse)), (400, typeof(ErrorResponse)), (409, typeof(ErrorResponse))],
            ErrorResponses(endpoint));
        Assert.Equal(
            [("test_1003", 404, typeof(ErrorResponse)), ("test_1001", 400, typeof(ErrorResponse)), ("test_1006", 409, typeof(ErrorResponse))],
            declared.Errors.Select(error => (error.Error.Code, error.StatusCode, error.ResponseType)));
    }

    [Fact]
    public async Task ProducesErrorSet_WithUseEnvelope_DocumentsEnvelopes()
    {
        Endpoint endpoint = await TestApp.EndpointAsync(
            app => app
                .MapGet("/", IResult () => TypedResults.Ok())
                .ProducesErrorSet<SendOrderError>(),
            options => options.UseEnvelope = true);

        DeclaredErrorsMetadata declared = endpoint.Metadata.GetRequiredMetadata<DeclaredErrorsMetadata>();

        Assert.Equal([(409, typeof(ResultEnvelope))], ErrorResponses(endpoint));
        Assert.All(declared.Errors, error => Assert.Equal(typeof(ResultEnvelope), error.ResponseType));
    }

    [Fact]
    public async Task ProducesErrorSet_WithOwnEnvelope_DocumentsTheStatusWithoutType()
    {
        Endpoint endpoint = await TestApp.EndpointAsync(
            app => app
                .MapGet("/", IResult () => TypedResults.Ok())
                .ProducesErrorSet<SendOrderError>(),
            options =>
            {
                options.UseEnvelope = true;
                options.CreateEnvelope = content => new { content.Code };
            });

        IProducesResponseTypeMetadata conflict = Assert.Single(
            endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            response => response.StatusCode == StatusCodes.Status409Conflict);

        Assert.Null(conflict.Type);
        Assert.All(
            endpoint.Metadata.GetRequiredMetadata<DeclaredErrorsMetadata>().Errors,
            error => Assert.Null(error.ResponseType));
    }

    [Fact]
    public async Task ProducesErrorSet_WithResultHandler_DocumentsBothSets()
    {
        Endpoint endpoint = await TestApp.EndpointAsync(app => app
            .MapGet("/", () => Result<OrderDto, GetOrderError>.Failure(new OrderClosed()))
            .WithResultResponses()
            .ProducesErrorSet<SendOrderError>());

        Assert.Equal(
            ["test_1001", "test_1002", "test_1003", "test_1004", "test_1006"],
            endpoint.Metadata.GetOrderedMetadata<DeclaredErrorsMetadata>()
                .SelectMany(declared => declared.Errors)
                .Select(error => error.Error.Code)
                .Order(StringComparer.Ordinal));
        Assert.Single(
            endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            response => response.StatusCode == StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task ProducesErrorSet_WithOwnResponse_KeepsTheEndpointsOwn()
    {
        Endpoint endpoint = await TestApp.EndpointAsync(app => app
            .MapGet("/", IResult () => TypedResults.Ok())
            .Produces<OrderDto>(StatusCodes.Status409Conflict)
            .ProducesErrorSet<SendOrderError>());

        IProducesResponseTypeMetadata conflict = Assert.Single(
            endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>(),
            response => response.StatusCode == StatusCodes.Status409Conflict);

        Assert.Equal(typeof(OrderDto), conflict.Type);
    }

    [Fact]
    public async Task ProducesErrorSet_WithUnionOfNonErrors_ThrowsWhereMapped()
    {
        await using WebApplication app = WebApplication.CreateBuilder().Build();
        RouteHandlerBuilder endpoint = app.MapGet("/", IResult () => TypedResults.Ok());

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            endpoint.ProducesErrorSet<NotAnErrorSet>);

        Assert.Contains(nameof(NotAnErrorSet), exception.Message, StringComparison.Ordinal);
    }

    private static IEnumerable<(int StatusCode, Type? Type)> ErrorResponses(Endpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>()
            .Where(response => response.StatusCode >= 400)
            .Select(response => (response.StatusCode, response.Type));
}
