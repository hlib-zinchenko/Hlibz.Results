#!/usr/bin/env bash
# Checks the packed packages the way consumers get them, which the tests can't: they reference
# the source generator as a project analyzer. Builds, outside the repo, two projects that get
# Hlibz.Results only indirectly:
#   Shared  a class library referencing only Hlibz.Results.FluentValidation,
#   Api     a console app referencing only Shared,
# each declaring one error code, then runs Api: each assembly must carry the generated list of
# its codes, and an ErrorCodeCatalog of both must list both. The catalog throws for an assembly
# the generator didn't run on, so a generator that is missing from the package, doesn't load or
# doesn't reach an indirect consumer fails the check.
#
# Usage: check-packed-generator.sh <directory holding the .nupkg files>
set -euo pipefail

artifacts="$(cd "$1" && pwd)"
package="$(ls "$artifacts"/Hlibz.Results.[0-9]*.nupkg)"
version="$(basename "$package" .nupkg)"
version="${version#Hlibz.Results.}"

# The physical path: restore drops a project reference reached through a symlink (macOS /var).
work="$(cd "$(mktemp -d)" && pwd -P)"
trap 'rm -rf "$work"' EXIT
# Its own package cache: the packed version must never land in the machine-wide one.
export NUGET_PACKAGES="$work/packages"

# The packages only from the fresh pack; FluentValidation from nuget.org.
cat > "$work/nuget.config" <<EOF
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
EOF

mkdir "$work/Shared" "$work/Api"

cat > "$work/Shared/Shared.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Hlibz.Results.FluentValidation" Version="$version" />
  </ItemGroup>
</Project>
EOF

cat > "$work/Shared/OrdersErrorCodes.cs" <<'EOF'
using Hlibz.Results;

namespace Shared;

public static class OrdersErrorCodes
{
    public static readonly ErrorCode Closed = new("orders_1001", "The order is closed.");
}
EOF

cat > "$work/Api/Api.csproj" <<'EOF'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../Shared/Shared.csproj" />
  </ItemGroup>
</Project>
EOF

cat > "$work/Api/Program.cs" <<'EOF'
using System.Reflection;
using Hlibz.Results;
using Shared;

Assembly[] assemblies = [typeof(ApiErrorCodes).Assembly, typeof(OrdersErrorCodes).Assembly];
ErrorCodeCatalog catalog = new(assemblies);
catalog.Validate();
int[] listed = [.. assemblies.Select(assembly => assembly.GetCustomAttribute<ErrorCodeManifestAttribute>()?.ErrorCodes.Count ?? -1)];
Console.WriteLine($"Error codes listed per assembly: {string.Join(", ", listed)}; catalog: {string.Join(", ", catalog.All.Select(code => code.Code))}");
return listed.All(count => count == 1) && catalog.All.Count == 2 ? 0 : 1;

internal static class ApiErrorCodes
{
    public static readonly ErrorCode Down = new("api_1001", "The API is down.");
}
EOF

echo "Checking Hlibz.Results(.FluentValidation) $version from $artifacts"
dotnet run --project "$work/Api/Api.csproj"
