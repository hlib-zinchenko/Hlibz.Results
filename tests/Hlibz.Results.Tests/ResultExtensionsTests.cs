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
        Result<string> result = Result<int>.Failure(new OrderNotFound()).Map<int, string>(
            _ => throw new InvalidOperationException("Must not run."));

        Assert.IsType<OrderNotFound>(result.Error);
    }

    [Fact]
    public void Map_WithErrorSet_KeepsTheSetAndTheDeclaredError()
    {
        Result<int, PayOrderError> paid = new OrderClosed(Guid.Empty);

        Result<string, PayOrderError> result = paid.Map(value => $"#{value}");

        Assert.IsType<OrderClosed>(result.DeclaredError?.Value);
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
            .Bind(value => value > 3 ? new OrderTooLarge(value, 3) : Result<int>.Success(value));

        Assert.IsType<OrderTooLarge>(result.Error);
    }

    [Fact]
    public void Bind_WithErrorSet_RunsNextStepWithTheSameSet()
    {
        Result<int, PayOrderError> result = Result<int, PayOrderError>.Success(4)
            .Bind(value => value > 3 ? new OrderNotFound() : Result<int, PayOrderError>.Success(value));

        Assert.IsType<OrderNotFound>(result.DeclaredError?.Value);
    }

    [Fact]
    public void Bind_WithNonGenericFailure_PassesFailureOn()
    {
        Result<int> result = Result.Failure(new Forbidden()).Bind(() => Result<int>.Success(1));

        Assert.IsType<Forbidden>(result.Error);
    }
}
