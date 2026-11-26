#!/usr/bin/env bash
# Checks the build-time error code files the way consumers get them, which the tests can't: they
# call the tool's code in-process, and the samples run it from its project's output. Builds,
# outside the repo, web apps referencing only the packed Hlibz.Results.AspNetCore: the package's
# buildTransitive target must run the packed tool, which must stop the app at Build() (past it,
# the app throws) and write the default and the named catalog, on each target framework, to
# ErrorCodesDirectory or obj/; must run the app in the environment ErrorCodesGenerateFilesOptions
# names; and must do nothing unless ErrorCodesGenerateFiles is on, or when
# ErrorCodesGenerateFilesOnBuild turns it off for one build (as CI would).
#
# Usage: check-packed-error-code-files.sh <directory holding the .nupkg files>
set -euo pipefail

artifacts="$(cd "$1" && pwd)"
package="$(ls "$artifacts"/Hlibz.Results.AspNetCore.[0-9]*.nupkg)"
version="$(basename "$package" .nupkg)"
version="${version#Hlibz.Results.AspNetCore.}"

work="$(cd "$(mktemp -d)" && pwd -P)"
trap 'rm -rf "$work"' EXIT
# Its own package cache: the packed version must never land in the machine-wide one.
export NUGET_PACKAGES="$work/packages"

cat > "$work/nuget.config" <<CONFIG
<configuration>
  <packageSources>
    <clear />
    <add key="artifacts" value="$artifacts" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="artifacts">
      <package pattern="Hlibz.Results" />
      <package pattern="Hlibz.Results.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
CONFIG

# make_app <directory> <target framework> <properties> [<statement before Build()>]
make_app() {
  mkdir "$1"
  cat > "$1/Api.csproj" <<CSPROJ
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>$2</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    $3
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Hlibz.Results.AspNetCore" Version="$version" />
  </ItemGroup>
</Project>
CSPROJ

  cat > "$1/Program.cs" <<'CS'
using Hlibz.Results;
using Hlibz.Results.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
// Before Build.
builder.Services
    .AddErrorCodeCatalog(typeof(ApiDown).Assembly)
    .AddErrorCodeCatalog("admin", typeof(ApiDown).Assembly);
WebApplication app = builder.Build();
app.MapErrorCodes();
throw new InvalidOperationException("The app ran past Build() at build time.");

[Error("api_1001", ResultStatus.Unavailable, "The API is down.")]
public sealed record ApiDown : Error;
CS
  if [ -n "${4:-}" ]; then
    STATEMENT="$4" perl -0pi -e 's|// Before Build\.|$ENV{STATEMENT}|' "$1/Program.cs"
  fi
}

# expect_files <directory> <label>
expect_files() {
  for file in error-codes.json error-codes.admin.json; do
    grep -q '"code": "api_1001"' "$1/$file" || { echo "$2: $1/$file is missing or lacks api_1001"; exit 1; }
  done
  echo "$2: wrote $(ls "$1" | grep error-codes | tr '\n' ' ')"
}

# expect_none <directory> <label>
expect_none() {
  if ls "$1"/error-codes*.json > /dev/null 2>&1; then
    echo "$2: wrote error code files it shouldn't have"
    exit 1
  fi
  echo "$2: wrote nothing, as expected"
}

echo "Checking build-time error code files of Hlibz.Results.AspNetCore $version from $artifacts"

for framework in net11.0; do
  make_app "$work/On-$framework" "$framework" \
    "<ErrorCodesGenerateFiles>true</ErrorCodesGenerateFiles><ErrorCodesDirectory>../client/\$(TargetFramework)</ErrorCodesDirectory>"
  dotnet build "$work/On-$framework/Api.csproj"
  expect_files "$work/client/$framework" "$framework, ErrorCodesDirectory"
done

make_app "$work/Default" net11.0 "<ErrorCodesGenerateFiles>true</ErrorCodesGenerateFiles>"
dotnet build "$work/Default/Api.csproj"
expect_files "$work/Default/obj" "ErrorCodesDirectory unset (obj/)"

# The app only starts in Development, as one whose Production config lacks its secrets.
make_app "$work/Environment" net11.0 \
  "<ErrorCodesGenerateFiles>true</ErrorCodesGenerateFiles><ErrorCodesGenerateFilesOptions>--environment Development</ErrorCodesGenerateFilesOptions>" \
  'if (!builder.Environment.IsDevelopment()) { throw new InvalidOperationException($"Started in {builder.Environment.EnvironmentName}."); }'
dotnet build "$work/Environment/Api.csproj"
expect_files "$work/Environment/obj" "--environment Development"

make_app "$work/Off" net11.0 ""
dotnet build "$work/Off/Api.csproj"
expect_none "$work/Off/obj" "ErrorCodesGenerateFiles unset"

rm -rf "$work/client/net11.0"
dotnet build "$work/On-net11.0/Api.csproj" --no-incremental -p:ErrorCodesGenerateFilesOnBuild=false
expect_none "$work/client/net11.0" "-p:ErrorCodesGenerateFilesOnBuild=false"
