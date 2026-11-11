namespace Hlibz.Results.Tests;

public sealed class ResultExtensionsTests
{
    [Fact]
    public void Map_WithSuccess_TransformsValue()
    {
        Result<string> result = Result<int>.Success(2).Map(value => $"#{value}");

        Assert.Equal("#2", result.Value);
    }

    [Fact]
    public void Map_WithFailure_PassesFailureOnWithoutCallingMap()
    {
        Result<string> result = Result<int>.NotFound("Gone.").Map<int, string>(
            _ => throw new InvalidOperationException("Must not run."));

        Assert.Equal(ResultStatus.NotFound, result.Status);
        Assert.Equal("Gone.", result.ErrorMessage);
    }

    [Fact]
    public async Task MapAsync_WithSuccess_TransformsValue()
    {
        Result<int> result = await Task.FromResult(Result<int>.Success(2)).MapAsync(v => v * 10);

        Assert.Equal(20, result.Value);
    }

    [Fact]
    public void Bind_WithSuccess_RunsNextStep()
    {
        Result<int> result = Result<int>.Success(4)
            .Bind(value => value > 3 ? Result<int>.Conflict("Too big.") : Result<int>.Success(value));

        Assert.Equal(ResultStatus.Conflict, result.Status);
    }

    [Fact]
    public void Bind_WithNonGenericFailure_PassesFailureOn()
    {
        Result<int> result = Result.Forbidden("No.").Bind(() => Result<int>.Success(1));

        Assert.Equal(ResultStatus.Forbidden, result.Status);
        Assert.Equal("No.", result.ErrorMessage);
    }
}
