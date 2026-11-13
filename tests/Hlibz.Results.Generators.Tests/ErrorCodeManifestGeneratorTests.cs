using System.Collections.Immutable;
using System.Reflection;

using Microsoft.CodeAnalysis;

namespace Hlibz.Results.Generators.Tests;

public sealed class ErrorCodeManifestGeneratorTests
{
    private const string Codes = """
        namespace App;

        public sealed record AdminErrorCode(string Code, string Message)
            : ErrorCode($"admin_{Code}", Message);

        public static class OrdersErrorCodes
        {
            public static readonly ErrorCode Closed = new("orders_1001", "The order is closed.");

            public static ErrorCode Paid { get; } = new("orders_1002", "The order is paid.");

            public static readonly ErrorCode ClosedAlias = Closed;

            public static readonly AdminErrorCode @class = new("1001", "A keyword-named code.");

            internal static readonly ErrorCode Internal = new("hidden_1", "Not public.");

            public static ErrorCode Computed => new("orders_1003", "Read on each access.");
        }

        internal sealed class InternalCodes
        {
            public static readonly ErrorCode Code = new("internal_1001", "From an internal class.");

            public readonly ErrorCode Instance = new("instance_1", "Not static.");
        }

        public sealed class Outer
        {
            private static class Hidden
            {
                public static readonly ErrorCode Code = new("hidden_1001", "Unreachable.");
            }
        }

        public static class Generic<T>
        {
            public static readonly ErrorCode Code = new("generic_1", "In a generic type.");
        }

        public readonly struct NotAClass
        {
            public static readonly ErrorCode Code = new("struct_1", "On a struct.");
        }
        """;

    [Fact]
    public void Generate_WithErrorCodes_ListsTheOnesTheCatalogCollects()
    {
        (string? manifest, _) = Compiler.Generate(Codes);

        Assert.NotNull(manifest);
        string[] listed =
        [
            .. manifest.Split('\n')
                .Where(line => line.Contains("static () =>", StringComparison.Ordinal))
                .Select(line => line.Trim()),
        ];
        Assert.Equal(
            [
                "static () => global::App.OrdersErrorCodes.Closed,",
                "static () => global::App.OrdersErrorCodes.Paid,",
                "static () => global::App.OrdersErrorCodes.ClosedAlias,",
                "static () => global::App.OrdersErrorCodes.@class,",
                "static () => global::App.OrdersErrorCodes.Computed,",
                "static () => global::App.InternalCodes.Code,",
            ],
            listed);
    }

    [Fact]
    public void Catalog_OnGeneratedAssembly_ListsCodesSortedWithAliasesOnce()
    {
        (_, Compilation output) = Compiler.Generate(Codes);

        ErrorCodeCatalog catalog = new([Compiler.Load(output)]);

        Assert.Equal(
            ["admin_1001", "internal_1001", "orders_1001", "orders_1002", "orders_1003"],
            catalog.All.Select(errorCode => errorCode.Code));
    }

    [Fact]
    public void Catalog_WithDuplicateCodes_Throws()
    {
        (_, Compilation output) = Compiler.Generate("""
            namespace App;

            public static class Codes
            {
                public static readonly ErrorCode First = new("dup_1", "Same.");

                public static readonly ErrorCode Second = new("dup_1", "Same.");
            }
            """);
        ErrorCodeCatalog catalog = new([Compiler.Load(output)]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(catalog.Validate);

        Assert.Contains("dup_1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_OnAssemblyWithoutGenerator_Throws()
    {
        Assembly assembly = Compiler.Load(Compiler.Compile("""
            namespace App;

            public static class Codes
            {
                public static readonly ErrorCode First = new("code_1", "First.");
            }
            """));
        ErrorCodeCatalog catalog = new([assembly]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(catalog.Validate);

        Assert.Contains("source generator didn't run", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_OnAssemblyNotReferencingResults_HasNoCodes()
    {
        ErrorCodeCatalog catalog = new([typeof(object).Assembly, typeof(ErrorCode).Assembly]);

        Assert.Empty(catalog.All);
    }

    [Fact]
    public void Generate_WithoutLibraryReference_WritesNothing()
    {
        (string? manifest, _) = Compiler.Generate("namespace App; public static class Plain { }", referenceLibrary: false);

        Assert.Null(manifest);
    }

    [Fact]
    public async Task Analyze_WithCodeInPrivateNestedClass_ReportsHres001()
    {
        ImmutableArray<Diagnostic> diagnostics = await Compiler.AnalyzeAsync(Codes);

        Diagnostic diagnostic = Assert.Single(diagnostics);
        Assert.Equal("HRES001", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Warning, diagnostic.Severity);
        Assert.StartsWith(
            "App.Outer.Hidden.Code is left out of every error code catalog",
            diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
            StringComparison.Ordinal);
    }
}
