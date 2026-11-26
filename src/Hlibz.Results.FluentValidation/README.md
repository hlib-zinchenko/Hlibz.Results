![Hlibz.Results.FluentValidation icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Results/main/docs/icon.png)

# Hlibz.Results.FluentValidation

[![NuGet](https://img.shields.io/nuget/v/Hlibz.Results.FluentValidation.svg)](https://www.nuget.org/packages/Hlibz.Results.FluentValidation)
[![CI](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml)

Turns a failed FluentValidation `ValidationResult` into a
[Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results) `ValidationFailed` error carrying
its validation errors, grouped by property path as FluentValidation reports it
(`Items[0].Quantity`).

```bash
dotnet add package Hlibz.Results.FluentValidation
```

```csharp
using Hlibz.Results.FluentValidation;

ValidationResult validation = await validator.ValidateAsync(request);
if (!validation.IsValid)
{
    return validation.ToValidationFailed(); // converts to any result, whatever its error set
}
```

`ValidationFailed` is an ambient error (status `Invalid`, code `validation_failed`): every
`Result<T, TError>` accepts it. `ToErrors()` gives the raw
`IReadOnlyDictionary<string, string[]>`. Both throw when the validation passed.

Works with FluentValidation 11.0.0 and later: reference the version you want in your app and
NuGet uses it.

## Related packages

- [Hlibz.Results](https://www.nuget.org/packages/Hlibz.Results): the `Result` types.
- [Hlibz.Results.AspNetCore](https://www.nuget.org/packages/Hlibz.Results.AspNetCore): results
  to HTTP responses with one error body, for minimal APIs and controllers.

Full documentation and samples: [github.com/hlib-zinchenko/Hlibz.Results](https://github.com/hlib-zinchenko/Hlibz.Results).
