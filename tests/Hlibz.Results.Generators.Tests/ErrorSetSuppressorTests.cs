namespace Hlibz.Results.Generators.Tests;

public sealed class ErrorSetSuppressorTests
{
    private const string Errors = """
        [Error("x_1", ResultStatus.NotFound, "Missing.")]
        public sealed record Missing(int Id) : Error;

        [Error("x_2", ResultStatus.Conflict, "Taken.")]
        public sealed record Taken : Error;

        """;

    [Fact]
    public async Task ErrorSet_SuppressesCA1815()
    {
        IReadOnlyDictionary<string, bool> reported = await Compiler.SuppressAsync(Errors + """
            public union GetError(Missing);

            public union SaveError(Missing, Taken);
            """);

        Assert.True(reported["GetError"]);
        Assert.True(reported["SaveError"]);
    }

    [Fact]
    public async Task ErrorSet_NestedInAClass_SuppressesCA1815()
    {
        IReadOnlyDictionary<string, bool> reported = await Compiler.SuppressAsync(Errors + """
            public static class Orders
            {
                public union GetError(Missing);
            }
            """);

        Assert.True(reported["GetError"]);
    }

    [Theory]
    [InlineData("public union Mixed(Missing, int);")]
    [InlineData("public union Numbers(int, long);")]
    [InlineData("public struct Point { public int X; }")]
    public async Task OtherStruct_KeepsCA1815(string declaration)
    {
        IReadOnlyDictionary<string, bool> reported = await Compiler.SuppressAsync(Errors + declaration);

        Assert.False(Assert.Single(reported).Value);
    }

    [Fact]
    public async Task Union_WithoutHlibzResults_KeepsCA1815()
    {
        IReadOnlyDictionary<string, bool> reported = await Compiler.SuppressAsync(
            "public union Numbers(int, long);",
            referenceLibrary: false);

        Assert.False(reported["Numbers"]);
    }
}
