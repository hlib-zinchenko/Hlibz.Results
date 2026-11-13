# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

Three NuGet packages released together from one tag:

- `src/Hlibz.Results`: `Result`/`Result<T>`, `ResultStatus`, `ErrorCode`, `ResultExtensions`
  (`Map`/`MapAsync`/`Bind`), `ErrorCodeCatalog`. No dependencies, no ASP.NET.
- `src/Hlibz.Results.AspNetCore`: result → HTTP for minimal APIs (`HttpResultExtensions`) and
  controllers (`ActionResultExtensions`), `ErrorResponse`, opt-in `ResultEnvelope`,
  `ResultHttpOptions` (`AddResultErrorResponses`), `WithResultResponses()`/`WithResultEnvelopes()`
  (handlers return a `Result` as is: an endpoint filter sends it as `ToHttpResult()`/
  `ToEnvelopeHttpResult()` would, passing anything else through, and a `Finally` convention swaps
  the inferred `200 Result<T>` for `200 T`/`200 ResultEnvelope<T>`; `Internal/ResultEndpoints`),
  the auth gate's error body
  (`Internal/ErrorResponseAuthorizationResultHandler`), `WriteErrorAsync`, `AddErrorCodeCatalog` +
  `MapErrorCodes`. Catalogs: a default one (`AddErrorCodeCatalog(assemblies)`, plain
  `IErrorCodeCatalog`) and any number of named ones (`AddErrorCodeCatalog(name, assemblies)`,
  keyed services), so each app/audience publishes only its own assemblies' codes
  (`MapErrorCodes(pattern, name)`). Every response shape is built in `Internal/ResultResponses`; minimal API and
  MVC results defer to it at execution time (`DeferredHttpResult`/`DeferredActionResult`) so they
  can read options and the right JSON settings from the request's services.
  Envelopes: built-in `ResultEnvelope`, or the API's own via `ResultHttpOptions.CreateEnvelope`
  (`EnvelopeContent` in, any object out); `EnvelopeAlwaysOk` sends envelope responses as 200
  with the real status inside (`ResultResponses.EnvelopeHttpStatusCode`). Both apply to the
  `ToEnvelope…` methods and, with `UseEnvelope`, to `WriteErrorAsync` and the auth gate.
- `src/Hlibz.Results.FluentValidation`: `ValidationResult` → failure, the caller picking the
  status: `ToErrorResult()` (`Error`) or `ToInvalidResult()` (`Invalid`), plus `<T>` versions;
  `ToErrors()` for the raw dictionary. Deliberately plain extension methods, not C# 14 static
  extensions on `Result`: BannedApiAnalyzers can't ban calls to C# 14 extension members, and
  plain ones work with any consumer C# version.

Validation keys are stored raw in `Result`; casing is an HTTP concern (`ValidationKeyFormatter`).
Likewise `ResultStatus` says what failed (`Error` = business rule / bad request, `Invalid` =
validation, `Unavailable` = dependency down) and `ResultHttpOptions.StatusCodes` maps it to HTTP
(defaults 400/400/503; teams remap `Invalid`/`Error` to 422). Every status code goes through
`ResultResponses.StatusCodeFor(status, options)`; adding a `ResultStatus` means a default in
`ResultHttpOptions.StatusCodes` and a message in `ResultMessages`.

- `samples/`: a shop (`Shop.Core` customer codes, `Shop.Admin` admin codes in a separate
  assembly) served by `Shop.MinimalApi` and `Shop.Controllers`, net11.0 only, never packed,
  referencing the library projects. Keep each app's `.http` file and `samples/README.md` in step
  with the responses.

## Requires

* The .NET SDK `global.json` pins (11.0.100, patches roll forward; Dependabot bumps it), plus
  the .NET 10 runtime to run both test targets. Pinned because warnings are errors and
  `AnalysisMode=All`: a new SDK feature band brings new analyzer rules.

## Commands

```bash
dotnet build
dotnet test
dotnet test -f net11.0
dotnet pack -c Release -o ./nupkg
dotnet run --project samples/Shop.MinimalApi    # :5180, try Shop.MinimalApi.http
```

## Conventions

StyleCop + Sonar + `AnalysisMode=All`, warnings as errors; every public member has XML docs
(CS1591 on for `src`). Explicit types, no `var`. Tests: xUnit v3 on Microsoft Testing Platform,
named `Subject_WithCondition_ExpectedOutcome`. Versions are central (`Directory.Packages.props`);
the shared NuGet metadata and `Version` live in `Directory.Build.props`, release by pushing a
`vX.Y.Z` tag (see Releasing).

Release policy: the three packages always ship together under one version (lockstep), even when
only one changed. Dependencies are minimums, not the latest: FluentValidation stays at the oldest
version the adapter supports (11.0.0; Dependabot ignores it) and the tests run against it, so a
new FluentValidation release needs no release here. Raise it only when the adapter needs a newer
API or a FluentValidation major breaks the members it uses.

Each package has its own `src/<Package>/README.md` (packed by `src/Directory.Build.props`, shown
on its nuget.org page): short, with absolute links, since relative ones break on nuget.org. The
root `README.md` is the full documentation; keep them in step when an API changes.

## Releasing

1. Set `Version` in `Directory.Build.props`, commit, then push the tag `vX.Y.Z`.
2. The user approves the `release` environment run.
3. Once the run succeeds and
   `https://api.nuget.org/v3-flatcontainer/<lowercase id>/index.json` lists the version, Claude
   creates the GitHub release that `PackageReleaseNotes` links to:
   `gh release create vX.Y.Z --verify-tag --title X.Y.Z [--prerelease when the version has a suffix] --notes-file <notes>`.
   Claude writes the notes from `git log <previous tag>..vX.Y.Z`: breaking changes first, then
   features and fixes, then one nuget.org link per package. Nothing is attached.
