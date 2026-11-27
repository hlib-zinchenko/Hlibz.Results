using Microsoft.CodeAnalysis;

namespace Hlibz.Results.Generators.Tests;

/// <summary>
/// The result types and the ambient errors can't be extended outside Hlibz.Results: an app
/// declares errors and error sets, nothing else.
/// </summary>
public sealed class ExtensionPointTests
{
    [Theory]
    [InlineData("public sealed class MyResult : Result { public MyResult() : base(null) { } }", "CS0122")]
    [InlineData("public sealed class MyResult : Result { }", "CS1729")]
    [InlineData("public sealed class MyResult<T> : Result<T> { public MyResult() : base(default, null) { } }", "CS0122")]
    [InlineData("public sealed class MyResult<T> : Result<T> { }", "CS1729")]
    [InlineData("public sealed class MyResult : Result<int, Set> { }", "CS0509")]
    [InlineData("[Error(\"x_1\", ResultStatus.Error, \"X.\")] public sealed record WriteConflict : AmbientError;", "CS9382")]
    public void Subclass_InAnotherAssembly_DoesNotCompile(string declaration, string expected)
    {
        string source = $$"""
            [Error("x_2", ResultStatus.NotFound, "Missing.")]
            public sealed record Missing : Error;

            #pragma warning disable CA1815
            public union Set(Missing);
            #pragma warning restore CA1815

            {{declaration}}
            """;

        string[] errors = ErrorIdsOf(Compiler.Compile(source));

        Assert.Equal([expected], errors);
    }

    [Fact]
    public void ErrorRecord_InAnotherAssembly_Compiles()
    {
        string[] errors = ErrorIdsOf(Compiler.Compile("""
            [Error("x_2", ResultStatus.NotFound, "Missing.")]
            public sealed record Missing(int Id) : Error;
            """));

        Assert.Empty(errors);
    }

    private static string[] ErrorIdsOf(Compilation compilation) =>
        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id)
            .Distinct()
            .ToArray();
}
