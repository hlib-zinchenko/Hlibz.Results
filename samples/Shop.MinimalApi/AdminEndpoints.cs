using Hlibz.Results.AspNetCore;

using Shop.Admin;

namespace Shop.MinimalApi;

/// <summary>
/// The admin app: the same conventions, gated as a group by the admin policy.
/// </summary>
public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder admin = app
            .MapGroup("api/admin")
            .RequireAuthorization(Policies.Admin)
            .WithTags("Admin");

        admin.MapGet(
            "orders",
            (AdminOrderService service) => service.GetAll().ToHttpResult());

        admin.MapPost(
            "orders/{id:guid}/refund",
            (Guid id, RefundRequest request, AdminOrderService service) =>
                service.Refund(id, request).ToHttpResult());

        admin.MapPut(
            "payment-provider",
            (PaymentProviderStatus status, AdminOrderService service) =>
                service.SetPaymentProvider(status).ToHttpResult());
    }
}
