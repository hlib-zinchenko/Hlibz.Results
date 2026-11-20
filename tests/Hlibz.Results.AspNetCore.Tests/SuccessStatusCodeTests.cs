using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class SuccessStatusCodeTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Result<OrderDto> Order => Result<OrderDto>.Success(new(7, "Ann"));

    [Fact]
    public async Task ToHttpResult_WithSuccessMappedTo202_SendsValueWith202()
    {
        using HttpClient client = await TestApp.StartAsync(
            app =>
            {
                app.MapGet("/plain", () => Order.ToHttpResult());
                app.MapGet("/envelope", () => Order.ToEnvelopeHttpResult());
            },
            options => options.StatusCodes[ResultStatus.Success] = StatusCodes.Status202Accepted);

        using HttpResponseMessage plain = await client.GetAsync("/plain", Ct);
        using HttpResponseMessage envelope = await client.GetAsync("/envelope", Ct);

        Assert.Equal(HttpStatusCode.Accepted, plain.StatusCode);
        Assert.Equal(7, (await plain.ReadJsonAsync()).GetProperty("order_id").GetInt32());
        Assert.Equal(HttpStatusCode.Accepted, envelope.StatusCode);
        Assert.Equal(202, (await envelope.ReadJsonAsync()).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task WithResultResponses_WithDeleteDefaultOf204_SendsEmpty204AndLeavesPostAt200()
    {
        using HttpClient client = await TestApp.StartAsync(
            app =>
            {
                RouteGroupBuilder orders = app.MapGroup("/orders").WithResultResponses();
                orders.MapDelete("/", () => Order);
                orders.MapPost("/", () => Order);
            },
            options => options.SuccessStatusCodes[HttpMethods.Delete] = StatusCodes.Status204NoContent);

        using HttpResponseMessage deleted = await client.DeleteAsync("/orders/", Ct);
        using HttpResponseMessage posted = await client.PostAsync("/orders/", null, Ct);

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Empty(await deleted.Content.ReadAsStringAsync(Ct));
        Assert.Equal(HttpStatusCode.OK, posted.StatusCode);
        Assert.Equal(7, (await posted.ReadJsonAsync()).GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task WithResultResponses_WithLowercaseMethodKey_UsesIt()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapDelete("/", () => Result.Success()).WithResultResponses(),
            options => options.SuccessStatusCodes["delete"] = StatusCodes.Status204NoContent);

        using HttpResponseMessage response = await client.DeleteAsync("/", Ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task WithResultResponses_WithFailureUnderMethodDefault_SendsFailureStatus()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapDelete("/", () => Result.NotFound()).WithResultResponses(),
            options => options.SuccessStatusCodes[HttpMethods.Delete] = StatusCodes.Status204NoContent);

        using HttpResponseMessage response = await client.DeleteAsync("/", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task WithSuccessStatusCode_OnEndpoint_BeatsMethodDefault()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapDelete("/", () => Order)
                .WithResultResponses()
                .WithSuccessStatusCode(StatusCodes.Status200OK),
            options => options.SuccessStatusCodes[HttpMethods.Delete] = StatusCodes.Status204NoContent);

        using HttpResponseMessage response = await client.DeleteAsync("/", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(7, (await response.ReadJsonAsync()).GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task WithSuccessStatusCode_OnGroup_AppliesUnlessTheEndpointSetsItsOwn()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
        {
            RouteGroupBuilder orders = app.MapGroup("/orders")
                .WithResultResponses()
                .WithSuccessStatusCode(StatusCodes.Status201Created);
            orders.MapPost("/group", () => Order);
            orders.MapPost("/endpoint", () => Order).WithSuccessStatusCode(StatusCodes.Status202Accepted);
        });

        using HttpResponseMessage group = await client.PostAsync("/orders/group", null, Ct);
        using HttpResponseMessage endpoint = await client.PostAsync("/orders/endpoint", null, Ct);

        Assert.Equal(HttpStatusCode.Created, group.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, endpoint.StatusCode);
    }

    [Fact]
    public async Task WithSuccessStatusCode_OnNestedGroups_InnerGroupWins()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
        {
            RouteGroupBuilder outer = app.MapGroup("/outer")
                .WithResultResponses()
                .WithSuccessStatusCode(StatusCodes.Status201Created);
            RouteGroupBuilder inner = outer.MapGroup("/inner")
                .WithSuccessStatusCode(StatusCodes.Status202Accepted);
            inner.MapPost("/", () => Order);
        });

        using HttpResponseMessage response = await client.PostAsync("/outer/inner/", null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task SuccessStatusCodeAttribute_OnHandler_BeatsGroup()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
        {
            RouteGroupBuilder orders = app.MapGroup("/orders")
                .WithResultResponses()
                .WithSuccessStatusCode(StatusCodes.Status201Created);
            orders.MapPost("/", AcceptOrder);
        });

        using HttpResponseMessage response = await client.PostAsync("/orders/", null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact]
    public async Task WithSuccessStatusCode_WithDirectToHttpResult_AppliesToo()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
        {
            app.MapPost("/value", () => Order.ToHttpResult())
                .WithSuccessStatusCode(StatusCodes.Status201Created);
            app.MapPost("/empty", () => Result.Success().ToHttpResult())
                .WithSuccessStatusCode(StatusCodes.Status201Created);
        });

        using HttpResponseMessage value = await client.PostAsync("/value", null, Ct);
        using HttpResponseMessage empty = await client.PostAsync("/empty", null, Ct);

        Assert.Equal(HttpStatusCode.Created, value.StatusCode);
        Assert.Equal(7, (await value.ReadJsonAsync()).GetProperty("order_id").GetInt32());
        Assert.Equal(HttpStatusCode.Created, empty.StatusCode);
        Assert.Empty(await empty.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task WithResultEnvelopes_WithMethodDefaultOf201_SendsEnvelopeWith201()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapPost("/", () => Order).WithResultEnvelopes(),
            options => options.SuccessStatusCodes[HttpMethods.Post] = StatusCodes.Status201Created);

        using HttpResponseMessage response = await client.PostAsync("/", null, Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(201, body.GetProperty("status").GetInt32());
        Assert.Equal(7, body.GetProperty("data").GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task WithResultEnvelopes_With204_FailsWhenTheEndpointIsBuilt()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestApp.ResponsesAsync(
                app => app.MapDelete("/", () => Result.Success()).WithResultEnvelopes(),
                options => options.SuccessStatusCodes[HttpMethods.Delete] = StatusCodes.Status204NoContent));

        Assert.Contains("204", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ToEnvelopeHttpResult_With204_Throws()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapDelete("/", () => Result.Success().ToEnvelopeHttpResult())
                .WithSuccessStatusCode(StatusCodes.Status204NoContent));

        await Assert.ThrowsAsync<InvalidOperationException>(() => client.DeleteAsync("/", Ct));
    }

    [Fact]
    public async Task SuccessStatusCodes_WithNon2xxCode_Throws()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapPost("/", () => Order.ToHttpResult()),
            options => options.SuccessStatusCodes[HttpMethods.Post] = StatusCodes.Status302Found);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.PostAsync("/", null, Ct));

        Assert.Contains("SuccessStatusCodes[\"POST\"]", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SuccessStatusCodeAttribute_WithNon2xxCode_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SuccessStatusCodeAttribute(StatusCodes.Status302Found));

    [Fact]
    public async Task ToCreatedHttpResult_WithSuccess_Sends201WithLocationAndValue()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapPost("/", () => Order.ToCreatedHttpResult(order => $"/orders/{order.OrderId}")));

        using HttpResponseMessage response = await client.PostAsync("/", null, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/orders/7", response.Headers.Location?.OriginalString);
        Assert.Equal(7, (await response.ReadJsonAsync()).GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task ToCreatedHttpResult_WithFailure_SendsErrorWithoutLocation()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapPost(
                "/",
                () => Result<OrderDto>.Conflict("Taken.").ToCreatedHttpResult(order => $"/orders/{order.OrderId}")));

        using HttpResponseMessage response = await client.PostAsync("/", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ToCreatedHttpResult_WithNullLocation_Sends201WithoutLocation()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapPost("/", () => Order.ToCreatedHttpResult(_ => null)));

        using HttpResponseMessage response = await client.PostAsync("/", null, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
    }

    [Fact]
    public async Task ToCreatedEnvelopeHttpResult_WithEnvelopeAlwaysOk_Sends200WithLocationAnd201Inside()
    {
        using HttpClient client = await TestApp.StartAsync(
            app => app.MapPost("/", () => Order.ToCreatedEnvelopeHttpResult(order => $"/orders/{order.OrderId}")),
            options => options.EnvelopeAlwaysOk = true);

        using HttpResponseMessage response = await client.PostAsync("/", null, Ct);
        JsonElement body = await response.ReadJsonAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/orders/7", response.Headers.Location?.OriginalString);
        Assert.Equal(201, body.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task WithCreatedLocation_WithResultResponses_Sends201WithLocation()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapPost("/", () => Order)
                .WithResultResponses()
                .WithCreatedLocation<OrderDto>(order => $"/orders/{order.OrderId}"));

        using HttpResponseMessage response = await client.PostAsync("/", null, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/orders/7", response.Headers.Location?.OriginalString);
        Assert.Equal(7, (await response.ReadJsonAsync()).GetProperty("order_id").GetInt32());
    }

    [Fact]
    public async Task WithCreatedLocation_WithResultEnvelopes_Sends201EnvelopeWithLocation()
    {
        using HttpClient client = await TestApp.StartAsync(app =>
            app.MapPost("/", () => Order)
                .WithResultEnvelopes()
                .WithCreatedLocation<OrderDto>(order => $"/orders/{order.OrderId}"));

        using HttpResponseMessage response = await client.PostAsync("/", null, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("/orders/7", response.Headers.Location?.OriginalString);
        Assert.Equal(201, (await response.ReadJsonAsync()).GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task WithCreatedLocation_WithOtherValueType_FailsWhenTheEndpointIsBuilt()
    {
        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestApp.ResponsesAsync(app =>
                app.MapPost("/", () => Result<int>.Success(7))
                    .WithResultResponses()
                    .WithCreatedLocation<OrderDto>(order => $"/orders/{order.OrderId}")));

        Assert.Contains("Result<OrderDto>", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WithCreatedLocation_WithResultWithoutValue_FailsWhenTheEndpointIsBuilt() =>
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => TestApp.ResponsesAsync(app =>
                app.MapPost("/", () => Result.Success())
                    .WithResultResponses()
                    .WithCreatedLocation<OrderDto>(order => $"/orders/{order.OrderId}")));

    [Fact]
    public async Task WithResultResponses_WithDeleteDefaultOf204_Documents204WithoutBody()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(
                app => app.MapDelete("/", () => Order).WithResultResponses(),
                options => options.SuccessStatusCodes[HttpMethods.Delete] = StatusCodes.Status204NoContent));

        Assert.Equal(StatusCodes.Status204NoContent, response.StatusCode);
        Assert.Null(response.Type);
    }

    [Fact]
    public async Task WithResultResponses_WithEndpointCodeOverGroupCode_DocumentsTheEndpointCode()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(
                app =>
                {
                    RouteGroupBuilder group = app.MapGroup("/orders")
                        .WithResultResponses()
                        .WithSuccessStatusCode(StatusCodes.Status202Accepted);
                    group.MapPost("/", () => Order).WithSuccessStatusCode(StatusCodes.Status201Created);
                },
                pattern: "/orders/"));

        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        Assert.Equal(typeof(OrderDto), response.Type);
    }

    [Fact]
    public async Task WithCreatedLocation_WithResultEnvelopes_Documents201Envelope()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(app =>
                app.MapPost("/", () => Order)
                    .WithResultEnvelopes()
                    .WithCreatedLocation<OrderDto>(order => $"/orders/{order.OrderId}")));

        Assert.Equal(StatusCodes.Status201Created, response.StatusCode);
        Assert.Equal(typeof(ResultEnvelope<OrderDto>), response.Type);
    }

    [Fact]
    public async Task WithResultEnvelopes_WithEnvelopeAlwaysOk_Documents200()
    {
        IProducesResponseTypeMetadata response = Assert.Single(
            await TestApp.ResponsesAsync(
                app => app.MapPost("/", () => Order)
                    .WithResultEnvelopes()
                    .WithSuccessStatusCode(StatusCodes.Status201Created),
                options => options.EnvelopeAlwaysOk = true));

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
    }

    [SuccessStatusCode(StatusCodes.Status202Accepted)]
    private static Result<OrderDto> AcceptOrder() => Order;
}
