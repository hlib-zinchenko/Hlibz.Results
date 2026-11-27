# CLAUDE.md

Guidance for Claude Code when working in this repository.

## What this is

Four NuGet packages released together from one tag (plus a build tool packed inside one):

- `src/Hlibz.Results`: typed errors. `Error` (abstract record: `Code`/`Status`/`Message` from the
  type's `[Error(code, status, message)]` via `ErrorDescriptor.For(GetType())`, all `[JsonIgnore]`;
  `Message` isn't virtual, errors override the protected `FormatMessage`, so it never goes out in
  the details; `PrintMembers` prints only `Code` of the base), the `closed` `AmbientError`s
  (`ValidationFailed`, `Unauthorized`, `Forbidden`, `Unavailable`: any result accepts them, no
  set declares them), `Result` (any error) / `Result<T>` / `Result<T, TError>` (`TError` a union
  of error types, `where TError : struct, IUnion`; implicit conversions from the value and from
  `Error`, the latter wrapping the error in the union through the generated
  `ErrorSetDescriptor`; `DeclaredError` is the union, null on success or for an ambient error),
  `Unit`, `ResultExtensions` (`Map`/`MapAsync`/`Bind`, set-keeping overloads), `ErrorCodeCatalog`
  (the given assemblies' generated lists plus Hlibz.Results' own ambient errors; duplicates across
  assemblies throw; an assembly referencing Hlibz.Results without a list throws). A union
  conversion doesn't chain into a user-defined one (CS0029), hence the conversion from `Error`.
  `Result.Accept`/`IResultVisitor` (internal, visible to AspNetCore) reach a `Result<T>`'s typed
  code without reflection, for Native AOT. No dependencies, no ASP.NET. The generator also runs on
  this project (ambient errors are listed the same way).
- `src/Hlibz.Results.Generators` (netstandard2.0, Roslyn 4.8: a union is a
  `UnionDeclarationSyntax : TypeDeclarationSyntax` and compiles to a struct implementing `IUnion`
  with one public one-parameter constructor per case, so no newer API is needed; packed inside
  Hlibz.Results under `analyzers/dotnet/cs` with `buildTransitive/Hlibz.Results.targets`):
  `ErrorManifestGenerator` writes each assembly's `ErrorManifestAttribute` (an `ErrorDescriptor`
  per described error type, an `ErrorSetDescriptor` per union of error types with a `switch` that
  wraps an error in the union); `ErrorAnalyzer` reports `HRES001`-`HRES006` (all errors: what
  would fail at runtime); `ErrorSymbols` holds the shared rules. The tests compile C# 15 in memory
  with Roslyn 5.12 (`VersionOverride`), the SDK's. CI's `.github/scripts/check-packed-generator.sh`
  builds an indirect consumer from the packed nupkg. Rules are tracked in
  `AnalyzerReleases.*.md` and the README's "Build errors".
- `src/Hlibz.Results.AspNetCore`: result → HTTP for minimal APIs (`HttpResultExtensions`) and
  controllers (`ActionResultExtensions`), `ErrorResponse`, opt-in `ResultEnvelope`,
  `ResultHttpOptions` (`AddResultErrorResponses`), `WithResultResponses()`/`WithResultEnvelopes()`
  (handlers return a `Result` as is: an endpoint filter sends it as `ToHttpResult()`/
  `ToEnvelopeHttpResult()` would, passing anything else through, and a `Finally` convention swaps
  the inferred `200 Result<T>` for `200 T`/`200 ResultEnvelope<T>`; `Internal/ResultEndpoints`),
  the auth gate's error body
  (`Internal/ErrorResponseAuthorizationResultHandler`, only on a 401/403, written as the ambient
  `Unauthorized`/`Forbidden`: a scheme's redirect, such as the cookie login 302, is left as it is), `WriteErrorAsync`, `AddErrorCodeCatalog` +
  `MapErrorCodes` (published as `ErrorCodeInfo`s, `Internal/ErrorCodeInfos`: code, HTTP status,
  message, details JSON schema written with strict number handling). An error's data goes out as
  `ErrorResponse.Details`/the envelope's `Details` (the error record, `ResultResponses.DetailsOf`:
  only with `HasDetails`, never for `ValidationFailed`, whose messages are `ValidationErrors`, and
  only when the JSON options can write the type, so a missing AOT registration drops the details
  instead of failing the response). `WithResultResponses`/`WithResultEnvelopes` document a
  `Result<T, TError>` handler's errors: a `ProducesResponseTypeMetadata` per status the set's errors
  go out with (unless the endpoint documents it) and `DeclaredErrorsMetadata` (each error with its
  status and body type) for Hlibz.OpenApi.Results. `Result<Unit, …>` succeeds without a body. Catalogs: a default one (`AddErrorCodeCatalog(assemblies)`, plain
  `IErrorCodeCatalog`) and any number of named ones (`AddErrorCodeCatalog(name, assemblies)`,
  keyed services, plus an `Internal/ErrorCodeCatalogName` service each since keyed ones can't be
  listed), so each app/audience publishes only its own assemblies' codes
  (`MapErrorCodes(pattern, name)`). Build-time files:
  `buildTransitive/Hlibz.Results.AspNetCore.targets` runs the tool after `Build`. Its properties
  mirror Microsoft.Extensions.ApiDescription.Server's one for one (`ErrorCodesGenerateFiles`,
  `ErrorCodesGenerateFilesOnBuild`, `ErrorCodesDirectory` defaulting to `obj/`,
  `ErrorCodesGenerateFilesOptions` with `--environment <Name>`, which the tool passes to the
  entry point and sets as `ASPNETCORE_ENVIRONMENT`/`DOTNET_ENVIRONMENT`, `GenerateErrorCodeFiles`
  target): keep it that way rather than adding settings of our own. The
  one difference is deliberate: `ErrorCodesGenerateFiles` is off by default, since every app
  returning results references this package. Every response shape is built in `Internal/ResultResponses` and written through `JsonTypeInfo`
  (`Internal/ResultsJsonContext`, source-generated metadata `AddResultErrorResponses` appends to
  the minimal-API and MVC JSON options, is what serializes them under Native AOT); minimal API and
  MVC results defer to it at execution time (`DeferredHttpResult`/`DeferredActionResult`) so they
  can read options and the right JSON settings from the request's services.
  Envelopes: built-in `ResultEnvelope`, or the API's own via `ResultHttpOptions.CreateEnvelope`
  (`EnvelopeContent` in, any object out); `EnvelopeAlwaysOk` sends envelope responses as 200
  with the real status inside (`ResultResponses.EnvelopeHttpStatusCode`). Both apply to the
  `ToEnvelope…` methods and, with `UseEnvelope`, to `WriteErrorAsync` and the auth gate.
- `src/Hlibz.Results.AspNetCore.ErrorCodeFiles` (net11.0 exe, never a package; packed inside
  Hlibz.Results.AspNetCore under `tools/`): runs in the app (`dotnet exec` on the app's deps.json
  and runtimeconfig.json), runs its entry point
  until the `HostBuilt` diagnostic event, aborts it with `HostAbortedException` (`AppServices`,
  as build-time OpenAPI and the EF tools do), then calls `Internal/ErrorCodeFiles.Write` by name
  (`ErrorCodeFilesWriter`: no compile-time reference, or the package would depend on the tool it
  packs). Files use the app's HTTP JSON options, indented, `\n` line ends, rewritten only when
  changed. The samples import the targets from `src` (`samples/Directory.Build.targets`); CI's
  `.github/scripts/check-packed-error-code-files.sh` runs them from the packed nupkg.
- `src/Hlibz.Results.FluentValidation`: `ToValidationFailed()` (a `ValidationFailed`, which any
  result accepts) and `ToErrors()` for the raw dictionary. Deliberately plain extension methods:
  BannedApiAnalyzers can't ban calls to extension members.
- `src/Hlibz.Results.Mcp` (on the MCP C# SDK `ModelContextProtocol`, a 2.4.0 minimum Dependabot
  ignores; no ASP.NET): errors as MCP tool errors with guidance. The SDK serializes a tool's
  return value before any filter sees it (no hook for the raw value, checked on 2.4.0), so tools
  unwrap with `ToolResultExtensions.ValueOrThrow()`/`ThrowIfFailed()`, which throw
  `ToolErrorException : McpException`. `WithToolErrors(configure)` (`IMcpServerBuilder`, once
  however often it's called; each `configure` adds to `ToolErrorOptions`) adds
  `Internal/ToolErrorFilter` (a call-tool filter: `isError`, `"code: guidance"` text,
  `structuredContent { code, details? }`, `Internal/ToolErrorResults`) and
  `Internal/ToolErrorCatalog`, both an `IPostConfigureOptions<McpServerOptions>` (writes each
  tool's errors into `Tool.Meta["hlibz/errors"]`: `[{ code, description, guidance? }]`,
  `ToolErrorOptions.MetaKey`) and an `IValidateOptions<McpServerOptions>` with `ValidateOnStart`
  (missing guidance, a placeholder that isn't a detail, a listed type that isn't an error). A
  tool's errors: its `ToolErrorsAttribute`s from `McpServerTool.Metadata` (`[ToolErrors<TErrorSet>]`
  through `ErrorSetDescriptor`, `[ToolError<TError>]`), then each `IToolErrorSource`, distinct.
  Details (`Internal/ToolErrorDetails`) mirror `ResultResponses.DetailsOf` (only `HasDetails`,
  only when `TryGetTypeInfo` succeeds) with the SDK's JSON options by default; detail names skip
  properties without a getter (the base's `[JsonIgnore]`d ones are listed that way);
  `ValidationFailed` goes out as `{ errors }`, keys through `Internal/ValidationKeyFormatter` (a
  copy of AspNetCore's). Hlibz.McpDocs.AspNetCore reads the same `_meta` key: the packages don't
  reference each other, the key and shape are the contract. Tests run a server and client in
  memory over pipes (`McpTestServer`); register static tool classes with `WithToolsFromAssembly`
  (`WithTools(typeof(X))` binds to the generic `WithTools<Type>(target)` overload).

Validation keys are stored raw in `ValidationFailed`; casing is an HTTP concern
(`ValidationKeyFormatter`). Likewise `ResultStatus` (on each `[Error]`) says what failed
(`Error` = business rule / bad request, `Invalid` = validation, `Unavailable` = dependency down)
and `ResultHttpOptions.StatusCodes` maps it to HTTP (defaults 400/400/503; teams remap
`Invalid`/`Error` to 422). Every status code goes through
`ResultResponses.StatusCodeFor(status, options)`; adding a `ResultStatus` means a default in
`ResultHttpOptions.StatusCodes`. Messages come from the errors (`ResultHttpOptions.SuccessMessage`
is a successful envelope's).

A `union` compiles to a struct without equality members, so CA1815 flags every error set under
`AnalysisMode=All`: suppress it on the union (`#pragma`), as the tests and sample do. A success's code is resolved
per request (`ResultResponses.CreateContext`, read from the endpoint by `DeferredHttpResult`/
`DeferredActionResult`) and per endpoint for OpenAPI (`ResultEndpoints.Document`), most specific
first: a `ToCreated…` call or `WithCreatedLocation` (201 + `Location`), the endpoint's last
`SuccessStatusCodeAttribute` (endpoint > handler attribute > inner group > outer group; action >
controller), `SuccessStatusCodes[method]`, `StatusCodes[Success]`. A 204 has no body and can't
carry an envelope.

- `samples/Cities.Api`: one minimal API (deleting a city user profiles use fails with the users'
  ids), never packed, referencing the library projects and no other Hlibz package.
  `error-codes/error-codes.json` is build output, committed; CI fails when a build changes it.
  Keep `Cities.Api.http` and `samples/README.md` in step with the responses.
- `tests/Hlibz.Results.AspNetCore.Tests.Admin`: a library with one more error, for catalogs of
  several assemblies (not a test project).

## Requires

* The .NET SDK `global.json` pins (11.0.100, patches roll forward; Dependabot bumps it). 3.x
  targets net11.0 only (C# 15: unions, `closed`); .NET 10 apps stay on 2.x. Pinned because
  warnings are errors and `AnalysisMode=All`: a new SDK feature band brings new analyzer rules.

## Commands

```bash
dotnet build
dotnet test
dotnet pack -c Release -o ./nupkg
dotnet run --project samples/Cities.Api    # :5180, try Cities.Api.http
```

## Conventions

StyleCop + Sonar + `AnalysisMode=All`, warnings as errors; every public member has XML docs
(CS1591 on for `src`). Explicit types, no `var`. Tests: xUnit v3 on Microsoft Testing Platform,
named `Subject_WithCondition_ExpectedOutcome`. Versions are central (`Directory.Packages.props`);
the shared NuGet metadata and `Version` live in `Directory.Build.props`, release by pushing a
`vX.Y.Z` tag (see Releasing).

Release policy: the four packages always ship together under one version (lockstep), even when
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
