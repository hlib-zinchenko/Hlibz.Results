namespace Hlibz.Results.Tests;

public sealed class ResultTests
{
    [Fact]
    public void Success_WithoutValue_IsSuccessWithNoError()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Success_WithValue_HoldsValue()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Success_WithNullForNullableType_IsSuccessWithNullValue()
    {
        Result<string?> result = Result<string?>.Success(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Failure_FromAnError_TakesStatusCodeAndMessageFromItsAttribute()
    {
        Result result = new OrderClosed(Guid.Empty);

        Assert.True(result.IsFailure);
        Assert.Equal(ResultStatus.Conflict, result.Status);
        Assert.Equal("orders_1001", result.Error.Code);
        Assert.Equal("The order is closed.", result.Error.Message);
    }

    [Fact]
    public void Failure_WithOverriddenMessage_UsesTheErrorsData()
    {
        Result<int> result = new OrderTooLarge(120, 100);

        Assert.Equal("The order has 120 lines; it can have 100.", result.Error?.Message);
        Assert.Equal(default, result.Value);
    }

    [Fact]
    public void Failure_WithNullError_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => Result<int>.Failure(null!));
    }

    [Fact]
    public void ValidationFailed_WithoutErrors_Throws()
    {
        Assert.Throws<ArgumentException>(() => new ValidationFailed(new Dictionary<string, string[]>()));
    }

    [Fact]
    public void AmbientErrors_HaveTheirOwnCodesAndStatuses()
    {
        Assert.Equal(
            [
                ("validation_failed", ResultStatus.Invalid),
                ("unauthorized", ResultStatus.Unauthorized),
                ("forbidden", ResultStatus.Forbidden),
                ("unavailable", ResultStatus.Unavailable),
            ],
            new Error[]
            {
                new ValidationFailed(new Dictionary<string, string[]> { ["Name"] = ["Required."] }),
                new Unauthorized(),
                new Forbidden(),
                new Unavailable(),
            }.Select(error => (error.Code, error.Status)));
    }

    [Fact]
    public void ErrorDescriptor_ForAnUndescribedType_Throws()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => ErrorDescriptor.For(typeof(string)));

        Assert.Contains("isn't a described error", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorDescriptor_TellsErrorsWithDataApart()
    {
        Assert.True(ErrorDescriptor.For(typeof(OrderClosed)).HasDetails);
        Assert.False(ErrorDescriptor.For(typeof(OrderNotFound)).HasDetails);
    }

    [Fact]
    public void ResultWithErrors_FromAnErrorOfTheSet_ExposesItForAnExhaustiveSwitch()
    {
        Result<int, PayOrderError> result = new OrderClosed(Guid.Empty);

        string text = result.DeclaredError switch
        {
            OrderNotFound => "not found",
            OrderClosed closed => $"closed {closed.OrderId}",
            null => "none",
        };

        Assert.Equal($"closed {Guid.Empty}", text);
        Assert.IsType<OrderClosed>(result.Error);
    }

    [Fact]
    public void ResultWithErrors_FromAnAmbientError_HasNoDeclaredError()
    {
        Result<int, PayOrderError> result = new Forbidden();

        Assert.True(result.IsFailure);
        Assert.Null(result.DeclaredError);
        Assert.Equal(ResultStatus.Forbidden, result.Status);
    }

    [Fact]
    public void ResultWithErrors_OnSuccess_HasNoDeclaredError()
    {
        Result<int, PayOrderError> result = 7;

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value);
        Assert.Null(result.DeclaredError);
    }

    [Fact]
    public void ResultWithErrors_FromAnErrorPassedOnAsError_ChecksItAgainstTheSet()
    {
        Error passedOn = new Unlisted();

        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => Result<int, PayOrderError>.Failure(passedOn));

        Assert.Contains("Unlisted isn't one of PayOrderError's errors", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ErrorToString_ShowsTheCodeAndTheErrorsData()
    {
        Assert.Equal(
            $"OrderClosed {{ Code = orders_1001, OrderId = {Guid.Empty} }}",
            new OrderClosed(Guid.Empty).ToString());
    }

    [Fact]
    public void ToString_DescribesTheOutcome()
    {
        Assert.Equal("Success", Result.Success().ToString());
        Assert.Equal("Success: 5", Result<int>.Success(5).ToString());
        Assert.Equal(
            "NotFound: There's no such order. [orders_1002]",
            Result.Failure(new OrderNotFound()).ToString());
    }
}
