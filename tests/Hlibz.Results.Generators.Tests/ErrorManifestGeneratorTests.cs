using System.Collections.Immutable;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

using Microsoft.CodeAnalysis;

namespace Hlibz.Results.Generators.Tests;

public sealed class ErrorManifestGeneratorTests
{
    private const string Errors = """
        namespace App;

        [Error("cities_1001", ResultStatus.NotFound, "There's no city with this id.")]
        public sealed record CityNotFound(Guid CityId) : Error;

        [Error("cities_1002", ResultStatus.Conflict, "The city is used by user profiles.")]
        public sealed record CityInUse(Guid CityId, IReadOnlyList<Guid> UserIds) : Error;

        [Error("cities_1003", ResultStatus.Error, "Say \"no\".")]
        internal sealed record class Quoted : Error;

        public abstract record CityError : Error;

        [Error("cities_1004", ResultStatus.Error, "Derived from an abstract error.")]
        public sealed record CityArchived : CityError;

        public union DeleteCityError(CityNotFound, CityInUse);

        public union NotErrors(int, string);

        public static class Cities
        {
            public static Result<Unit, DeleteCityError> Delete(int users) =>
                users > 0 ? new CityInUse(Guid.Empty, [Guid.Empty]) : Unit.Value;

            public static Result<Unit, DeleteCityError> Deny() => new Forbidden();

            public static Result<Unit, DeleteCityError> PassOn(Error error) => error;
        }
        """;

    [Fact]
    public void Generate_WithErrorsAndSets_ListsDescribedErrorsAndUnionsOfErrors()
    {
        (string? manifest, _) = Compiler.Generate(Errors);

        Assert.NotNull(manifest);
        Assert.Contains(
            "new global::Hlibz.Results.ErrorDescriptor(typeof(global::App.CityInUse), \"cities_1002\", (global::Hlibz.Results.ResultStatus)6, \"The city is used by user profiles.\", true)",
            manifest,
            StringComparison.Ordinal);
        Assert.Contains("\"Say \\\"no\\\".\", false)", manifest, StringComparison.Ordinal);
        Assert.Contains(
            "new global::Hlibz.Results.ErrorSetDescriptor(typeof(global::App.DeleteCityError), new global::System.Type[] { typeof(global::App.CityNotFound), typeof(global::App.CityInUse) }",
            manifest,
            StringComparison.Ordinal);
        Assert.DoesNotContain("NotErrors", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("global::App.CityError)", manifest, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorDescriptor_OnGeneratedAssembly_DescribesTheType()
    {
        Assembly assembly = Load();

        ErrorDescriptor descriptor = ErrorDescriptor.For(assembly.GetType("App.CityInUse")!);

        Assert.Equal("cities_1002", descriptor.Code);
        Assert.Equal(ResultStatus.Conflict, descriptor.Status);
        Assert.Equal("The city is used by user profiles.", descriptor.Message);
        Assert.True(descriptor.HasDetails);
    }

    [Fact]
    public void ErrorSetDescriptor_OnGeneratedAssembly_ListsTheCases()
    {
        Assembly assembly = Load();

        ErrorSetDescriptor descriptor = ErrorSetDescriptor.For(assembly.GetType("App.DeleteCityError")!);

        Assert.Equal(["CityNotFound", "CityInUse"], descriptor.ErrorTypes.Select(type => type.Name));
    }

    [Fact]
    public void ResultWithErrors_ReturningAnErrorOfTheSet_HoldsItAsTheDeclaredError()
    {
        Result result = Invoke(Load(), "Delete", 3);

        Assert.Equal("cities_1002", result.Error?.Code);
        Assert.Equal("CityInUse", DeclaredError(result)?.GetType().Name);
    }

    [Fact]
    public void ResultWithErrors_ReturningAnAmbientError_HasNoDeclaredError()
    {
        Result result = Invoke(Load(), "Deny");

        Assert.IsType<Forbidden>(result.Error);
        Assert.Null(DeclaredError(result));
    }

    [Fact]
    public void ResultWithErrors_PassingOnAnErrorOutsideTheSet_Throws()
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => Invoke(Load(), "PassOn", Activator.CreateInstance(Load().GetType("App.CityArchived")!)!));

        Assert.IsType<ArgumentException>(exception.InnerException);
        Assert.Contains("isn't one of DeleteCityError's errors", exception.InnerException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_OnGeneratedAssembly_ListsItsErrorsAndTheAmbientOnesSortedByCode()
    {
        ErrorCodeCatalog catalog = new([Load()]);

        Assert.Equal(
            ["cities_1001", "cities_1002", "cities_1003", "cities_1004", "concurrency_conflict", "forbidden", "rate_limited", "unauthorized", "unavailable", "validation_failed"],
            catalog.All.Select(error => error.Code));
    }

    [Fact]
    public void Catalog_WithTheSameCodeInTwoAssemblies_Throws()
    {
        const string Source = """
            namespace App;

            [Error("dup_1", ResultStatus.Error, "Same.")]
            public sealed record Duplicate : Error;
            """;
        Assembly first = Compiler.Load(Compiler.Generate(Source, assemblyName: "First").Output);
        Assembly second = Compiler.Load(Compiler.Generate(Source, assemblyName: "Second").Output);
        ErrorCodeCatalog catalog = new([first, second]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(catalog.Validate);

        Assert.Contains("dup_1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_OnAssemblyWithoutGenerator_Throws()
    {
        Assembly assembly = Compiler.Load(Compiler.Compile("""
            namespace App;

            [Error("code_1", ResultStatus.Error, "First.")]
            public sealed record First : Error;
            """));
        ErrorCodeCatalog catalog = new([assembly]);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(catalog.Validate);

        Assert.Contains("source generator didn't run", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Catalog_OnAssemblyNotReferencingResults_HasOnlyTheAmbientErrors()
    {
        ErrorCodeCatalog catalog = new([typeof(object).Assembly]);

        Assert.Equal(
            ["concurrency_conflict", "forbidden", "rate_limited", "unauthorized", "unavailable", "validation_failed"],
            catalog.All.Select(error => error.Code));
    }

    [Fact]
    public void Generate_WithoutLibraryReference_WritesNothing()
    {
        (string? manifest, _) = Compiler.Generate("namespace App; public static class Plain { }", referenceLibrary: false);

        Assert.Null(manifest);
    }

    [Fact]
    public async Task Analyze_WithValidErrorsAndSets_ReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await Compiler.AnalyzeAsync(Errors);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Analyze_WithErrorInPrivateNestedClass_ReportsHres001()
    {
        Diagnostic diagnostic = await SingleAsync("""
            namespace App;

            public sealed class Outer
            {
                [Error("hidden_1", ResultStatus.Error, "Hidden.")]
                private sealed record Hidden : Error;
            }
            """);

        Assert.Equal("HRES001", diagnostic.Id);
        Assert.StartsWith("App.Outer.Hidden can't be listed", Message(diagnostic), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Analyze_WithErrorWithoutAttribute_ReportsHres002()
    {
        Diagnostic diagnostic = await SingleAsync("namespace App; public sealed record Plain : Error;");

        Assert.Equal("HRES002", diagnostic.Id);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    [Fact]
    public async Task Analyze_WithTwoErrorsSharingACode_ReportsHres003()
    {
        Diagnostic diagnostic = await SingleAsync("""
            namespace App;

            [Error("dup_1", ResultStatus.Error, "One.")]
            public sealed record One : Error;

            [Error("dup_1", ResultStatus.Error, "Two.")]
            public sealed record Two : Error;
            """);

        Assert.Equal("HRES003", diagnostic.Id);
        Assert.Equal("App.Two and App.One share the error code 'dup_1'", Message(diagnostic));
    }

    [Fact]
    public async Task Analyze_WithResultOverAUnionOfNonErrors_ReportsHres004()
    {
        Diagnostic diagnostic = await SingleAsync("""
            namespace App;

            public union NotErrors(int, string);

            public static class Use
            {
                public static Result<int, NotErrors> Get() => 1;
            }
            """);

        Assert.Equal("HRES004", diagnostic.Id);
    }

    [Fact]
    public async Task Analyze_ReturningAnErrorOutsideTheSet_ReportsHres005()
    {
        const string Source = """
            namespace App;

            [Error("one_1", ResultStatus.Error, "One.")]
            public sealed record One : Error;

            [Error("two_1", ResultStatus.Error, "Two.")]
            public sealed record Two : Error;

            public union OnlyOne(One);

            public static class Use
            {
                public static Result<int, OnlyOne> Get(bool two) =>
                    two ? Result<int, OnlyOne>.Failure(new Two()) : new One();

                public static Result<int, OnlyOne> Convert() => new Two();
            }
            """;

        Diagnostic diagnostic = await SingleAsync(Source, count: 2);

        Assert.Equal("HRES005", diagnostic.Id);
        Assert.Equal("App.Two isn't one of App.OnlyOne's errors: add it to the union, or return one of its errors", Message(diagnostic));
    }

    [Fact]
    public async Task Analyze_WithSuccessStatus_ReportsHres006()
    {
        Diagnostic diagnostic = await SingleAsync("""
            namespace App;

            [Error("ok_1", ResultStatus.Success, "Not a failure.")]
            public sealed record Ok : Error;
            """);

        Assert.Equal("HRES006", diagnostic.Id);
    }

    private static Assembly Load() => Compiler.Load(Compiler.Generate(Errors).Output);

    private static Result Invoke(Assembly assembly, string method, params object[] arguments) =>
        (Result)assembly.GetType("App.Cities")!.GetMethod(method)!.Invoke(null, arguments)!;

    private static object? DeclaredError(Result result) =>
        result.GetType().GetProperty("DeclaredError")!.GetValue(result) is IUnion union ? union.Value : null;

    private static async Task<Diagnostic> SingleAsync(string source, int count = 1)
    {
        ImmutableArray<Diagnostic> diagnostics = await Compiler.AnalyzeAsync(source);
        Assert.Equal(count, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal(diagnostics[0].Id, diagnostic.Id));
        return diagnostics[0];
    }

    private static string Message(Diagnostic diagnostic) => diagnostic.GetMessage(CultureInfo.InvariantCulture);
}
