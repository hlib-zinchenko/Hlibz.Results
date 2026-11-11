using System.Security.Claims;

using Hlibz.Results.AspNetCore;

using Shop.Core.Orders;

namespace Shop.MinimalApi;

/// <summary>
/// The customer app. Each endpoint is one service call plus <c>ToHttpResult()</c>: 200 with the
/// value on success, otherwise the status the app maps the failure to and an error body.
/// </summary>
public static class OrderEndpoints
{
    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder orders = app
            .MapGroup("api/orders")
            .RequireAuthorization(Policies.Customer)
            .WithTags("Orders");

        orders.MapPost(
            string.Empty,
            (ClaimsPrincipal user, OrderService service) =>
                service.Create(user.CustomerId()).ToHttpResult());

        orders.MapGet(
            "{id:guid}",
            (Guid id, ClaimsPrincipal user, OrderService service) =>
                service.Get(id, user.CustomerId()).ToHttpResult());

        orders.MapPost(
            "{id:guid}/lines",
            (Guid id, AddLineRequest line, ClaimsPrincipal user, OrderService service) =>
                service.AddLine(id, user.CustomerId(), line).ToHttpResult());

        orders.MapPost(
                "{id:guid}/pay",
                async (
                    Guid id,
                    ClaimsPrincipal user,
                    OrderService service,
                    CancellationToken cancellationToken) =>
                    (await service.PayAsync(id, user.CustomerId(), cancellationToken))
                    .ToHttpResult())
            .RequireRateLimiting(Policies.Payments);
    }

    private static string CustomerId(this ClaimsPrincipal user) =>
        user.Identity?.Name ?? throw new InvalidOperationException("The endpoint requires a user.");
}
