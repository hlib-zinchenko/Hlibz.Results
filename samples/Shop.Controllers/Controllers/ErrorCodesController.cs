using Hlibz.Results;

using Microsoft.AspNetCore.Mvc;

namespace Shop.Controllers.Controllers;

/// <summary>
/// Publishes the catalogs from controllers: the default one is a plain service, a named one is a
/// keyed service. A real app would put the admin action behind an admin policy.
/// </summary>
[ApiController]
[Route("api")]
public sealed class ErrorCodesController : ControllerBase
{
    [HttpGet("error-codes")]
    public IReadOnlyList<ErrorCode> GetCustomerCodes(IErrorCodeCatalog catalog) => catalog.All;

    [HttpGet("admin/error-codes")]
    public IReadOnlyList<ErrorCode> GetAdminCodes(
        [FromKeyedServices("admin")] IErrorCodeCatalog catalog) => catalog.All;
}
