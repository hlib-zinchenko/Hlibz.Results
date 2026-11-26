![Hlibz.Results icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Results/main/docs/icon.png)

# Hlibz.Results

[![NuGet](https://img.shields.io/nuget/v/Hlibz.Results.svg)](https://www.nuget.org/packages/Hlibz.Results)
[![CI](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml)

`Result`, `Result<T>` and `Result<T, TError>` for returning expected failures instead of
throwing, with **typed errors**: each failure is a record carrying the data a client needs, and
an operation declares the errors it returns as a C# 15 `union`. No dependencies; .NET 11.

```bash
dotnet add package Hlibz.Results
```

```csharp
using Hlibz.Results;

[Error("cities_1001", ResultStatus.NotFound, "There's no city with this id.")]
public sealed record CityNotFound(Guid CityId) : Error;

[Error("cities_1002", ResultStatus.Conflict, "The city is used by user profiles.")]
public sealed record CityInUse(Guid CityId, int UserCount, IReadOnlyList<Guid> UserIds) : Error;

public union DeleteCityError(CityNotFound, CityInUse);

public Result<Unit, DeleteCityError> Delete(Guid id)
{
    if (!_cities.ContainsKey(id))
    {
        return new CityNotFound(id);
    }

    Guid[] users = UsersOf(id);
    return users.Length > 0 ? new CityInUse(id, users.Length, users) : Unit.Value;
}

string text = service.Delete(id).DeclaredError switch   // exhaustive: no _ arm
{
    CityNotFound => "No such city.",
    CityInUse inUse => $"Used by {inUse.UserCount} profiles.",
    null => "Deleted, or an ambient error.",
};
```

- `[Error]` gives each error type its code, what failed (`ResultStatus`) and its message.
- `Result<T, TError>` fails with an error of its set or an ambient one (`ValidationFailed`,
  `Unauthorized`, `Forbidden`, `Unavailable`), which the layers around an operation add.
  `Result<T>` and `Result` fail with any error.
- `Map`, `MapAsync` and `Bind` chain steps, keeping the error set.
- `ErrorCodeCatalog` lists every error type of the assemblies you give it, plus the ambient
  ones, sorted by code.

A source generator lists each assembly's error types and error sets as it compiles, so nothing
is scanned at runtime (the package is AOT-compatible), and an analyzer reports as build errors
what would fail at runtime: an error without `[Error]`, two sharing a code, an error returned
where the set doesn't list it.

## Related packages

- [Hlibz.Results.AspNetCore](https://www.nuget.org/packages/Hlibz.Results.AspNetCore): results
  to HTTP responses with one error body and the error's data as details, each declared error
  documented per endpoint.
- [Hlibz.Results.FluentValidation](https://www.nuget.org/packages/Hlibz.Results.FluentValidation):
  a failed `ValidationResult` as a `ValidationFailed` error.

Full documentation and samples: [github.com/hlib-zinchenko/Hlibz.Results](https://github.com/hlib-zinchenko/Hlibz.Results).
