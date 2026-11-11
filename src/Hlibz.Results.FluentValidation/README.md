![Hlibz.Results.FluentValidation icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Results/main/docs/icon.png)

# Hlibz.Results.FluentValidation

[![NuGet](https://img.shields.io/nuget/v/Hlibz.Results.FluentValidation.svg)](https://www.nuget.org/packages/Hlibz.Results.FluentValidation)
[![CI](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml)

Turns a failed FluentValidation `ValidationResult` into a
[Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results) failure carrying its validation
errors, grouped by property path as FluentValidation reports it (`Items[0].Quantity`).

```bash
dotnet add package Hlibz.Results.FluentValidation
```

```csharp
using Hlibz.Results.FluentValidation;

ValidationResult validation = await validator.ValidateAsync(request);
if (!validation.IsValid)
{
    return validation.ToInvalidResult<OrderDto>(); // status Invalid
    // or: return validation.ToErrorResult<OrderDto>(); // status Error, same validation errors
}
```

| Method | Status |
|---|---|
| `ToErrorResult()` / `ToErrorResult<T>()` | `Error`: for APIs that treat validation like any other bad request |
| `ToInvalidResult()` / `ToInvalidResult<T>()` | `Invalid`: for APIs that tell them apart (e.g. 400 vs 422) |
| `ToErrors()` | the raw `IReadOnlyDictionary<string, string[]>` |

All of them throw when the validation passed. They are plain extension methods, so they work
with any C# version, and you can keep a codebase to one status by banning the other with
[BannedApiAnalyzers](https://www.nuget.org/packages/Microsoft.CodeAnalysis.BannedApiAnalyzers):

```text
M:Hlibz.Results.FluentValidation.ValidationResultExtensions.ToInvalidResult(FluentValidation.Results.ValidationResult)
M:Hlibz.Results.FluentValidation.ValidationResultExtensions.ToInvalidResult``1(FluentValidation.Results.ValidationResult)
```

Works with FluentValidation 11.0.0 and later: reference the version you want in your app and
NuGet uses it.

Full documentation and samples: [https://github.com/hlib-zinchenko/Hlibz.Results](https://github.com/hlib-zinchenko/Hlibz.Results).
