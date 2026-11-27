![Hlibz.Results icon](https://raw.githubusercontent.com/hlib-zinchenko/Hlibz.Results/main/docs/icon.png)

# Hlibz.Results

[![NuGet](https://img.shields.io/nuget/v/Hlibz.Results.svg)](https://www.nuget.org/packages/Hlibz.Results)
[![CI](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml/badge.svg)](https://github.com/hlib-zinchenko/Hlibz.Results/actions/workflows/ci.yml)

`Result`, `Result<T>` and `Result<T, TError>` for returning expected failures instead of
throwing, with **typed errors**: each failure is a record carrying the data a client needs, and
an operation declares the errors it returns as a C# 15 `union`. No dependencies.

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
  `Unauthorized`, `Forbidden`, `ConcurrencyConflict`, `RateLimited`, `Unavailable`), which the
  layers around an operation add.
  `Result<T>` and `Result` fail with any error.
- `Map`, `MapAsync` and `Bind` chain steps, keeping the error set.
- `ErrorCodeCatalog` lists every error type of the assemblies you give it, plus the ambient
  ones, sorted by code.

A source generator lists each assembly's error types and error sets as it compiles, so nothing
is scanned at runtime (the package is AOT-compatible), and an analyzer reports as build errors
what would fail at runtime: an error without `[Error]`, two sharing a code, an error returned
where the set doesn't list it.

[Hlibz.Results.AspNetCore](https://www.nuget.org/packages/Hlibz.Results.AspNetCore) sends results
as HTTP responses with one error body and documents each declared error per endpoint;
[Hlibz.Results.FluentValidation](https://www.nuget.org/packages/Hlibz.Results.FluentValidation)
turns a failed `ValidationResult` into `ValidationFailed`;
[Hlibz.Results.Mcp](https://www.nuget.org/packages/Hlibz.Results.Mcp) fails MCP tools with them.

Targets .NET 11 (C# 15 unions). .NET 10 apps stay on 2.x.

## A family of packages

One of five packages I build to set up new .NET APIs the way I want them: Hlibz.Results,
Hlibz.UseCases, Hlibz.MinimalApi.Endpoints, Hlibz.OpenApi and Hlibz.Paging. Each works on its own,
and they work best together, as in [Hlibz.Stack](https://github.com/hlib-zinchenko/Hlibz.Stack). New
minor and major versions come often, a new major whenever .NET gives me a better way to do
something. Breaking changes only ever come in a major and are listed first in its
[release notes](https://github.com/hlib-zinchenko/Hlibz.Results/releases). You're welcome to use the
packages, and to [open an issue](https://github.com/hlib-zinchenko/Hlibz.Results/issues) when
something is wrong or missing.

Full documentation and samples: [github.com/hlib-zinchenko/Hlibz.Results](https://github.com/hlib-zinchenko/Hlibz.Results).
