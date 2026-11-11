![Hlibz.Results icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Results/main/docs/icon.png)

# Hlibz.Results

[![NuGet](https://img.shields.io/nuget/v/Hlibz.Results.svg)](https://www.nuget.org/packages/Hlibz.Results)
[![CI](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml)

A small `Result` / `Result<T>` for returning expected failures instead of throwing: a status,
a message, a machine-readable error code and validation errors. No dependencies.

```bash
dotnet add package Hlibz.Results
```

```csharp
using Hlibz.Results;

public sealed record OrdersErrorCode(string Code, string Message)
    : ErrorCode($"orders_{Code}", Message);

public static class OrdersErrorCodes
{
    public static readonly ErrorCode OrderClosed =
        new OrdersErrorCode("1001", "The order is closed.");
}

public async Task<Result<OrderDto>> AddLineAsync(Guid orderId, AddLine line)
{
    Order? order = await _orders.GetAsync(orderId);
    if (order is null)
    {
        return Result<OrderDto>.NotFound();
    }

    if (order.IsClosed)
    {
        return Result<OrderDto>.Conflict(OrdersErrorCodes.OrderClosed);
    }

    Result added = order.AddLine(line);
    if (added.IsFailure)
    {
        return Result<OrderDto>.FromFailed(added);
    }

    return Result<OrderDto>.Success(order.ToDto());
}
```

| Status | Factory |
|---|---|
| `Success` | `Success()` / `Success(value)` |
| `Error` | `Error(message)`, `Error(errorCode)`, `Error(validationErrors)` |
| `Invalid` | `Invalid(validationErrors)` |
| `NotFound` | `NotFound(message?)`, `NotFound(errorCode)` |
| `Unauthorized` | `Unauthorized(message?)`, `Unauthorized(errorCode)` |
| `Forbidden` | `Forbidden(message?)`, `Forbidden(errorCode)` |
| `Conflict` | `Conflict(message?)`, `Conflict(errorCode)` |
| `Unavailable` | `Unavailable(message?)`, `Unavailable(errorCode)` |

`Error` is a broken business rule or another bad request, `Invalid` a failed input validation;
validation errors can ride on either, so an API that doesn't tell them apart can use `Error`
only. `Result<T>` is a `Result`, and `IsSuccess` tells the compiler `Value` isn't null.
`FromFailed` passes a failure on as another type; `Map`, `MapAsync` and `Bind` chain steps.

`ErrorCodeCatalog` collects every public static `ErrorCode` declared in the assemblies you give
it, sorted, and throws when two share a code.

## Related packages

- [Hlibz.Results.AspNetCore](https://www.nuget.org/packages/Hlibz.Results.AspNetCore): results
  to HTTP responses with one error body, for minimal APIs and controllers.
- [Hlibz.Results.FluentValidation](https://www.nuget.org/packages/Hlibz.Results.FluentValidation):
  a failed `ValidationResult` to a failure.

Full documentation and samples: [https://github.com/hlib-zinchenko/Hlibz.Results](https://github.com/hlib-zinchenko/Hlibz.Results).
