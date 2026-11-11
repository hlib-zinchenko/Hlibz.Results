namespace Hlibz.Results.Tests;

public sealed class ResultTests
{
    private static readonly ErrorCode OrderClosed = new("orders_1001", "The order is closed.");

    [Fact]
    public void Success_WithoutValue_IsSuccessWithNoError()
    {
        Result result = Result.Success();

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(ResultStatus.Success, result.Status);
        Assert.Null(result.ErrorMessage);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Success_WithValue_HoldsValue()
    {
        Result<int> result = Result<int>.Success(42);

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

    [Theory]
    [InlineData(ResultStatus.Error)]
    [InlineData(ResultStatus.NotFound)]
    [InlineData(ResultStatus.Unauthorized)]
    [InlineData(ResultStatus.Forbidden)]
    [InlineData(ResultStatus.Conflict)]
    [InlineData(ResultStatus.Unavailable)]
    public void Failure_WithErrorCode_CarriesStatusCodeAndMessage(ResultStatus status)
    {
        foreach (Result result in new[] { FromCode(status), FromCodeTyped(status) })
        {
            Assert.True(result.IsFailure);
            Assert.Equal(status, result.Status);
            Assert.Equal("orders_1001", result.ErrorCode);
            Assert.Equal("The order is closed.", result.ErrorMessage);
        }
    }

    [Theory]
    [InlineData(ResultStatus.NotFound)]
    [InlineData(ResultStatus.Unauthorized)]
    [InlineData(ResultStatus.Forbidden)]
    [InlineData(ResultStatus.Conflict)]
    [InlineData(ResultStatus.Unavailable)]
    public void Failure_WithMessage_CarriesStatusAndMessageWithoutCode(ResultStatus status)
    {
        Result result = status switch
        {
            ResultStatus.NotFound => Result<int>.NotFound("Nope."),
            ResultStatus.Unauthorized => Result.Unauthorized("Nope."),
            ResultStatus.Forbidden => Result<int>.Forbidden("Nope."),
            ResultStatus.Unavailable => Result<int>.Unavailable("Nope."),
            _ => Result.Conflict("Nope."),
        };

        Assert.Equal(status, result.Status);
        Assert.Equal("Nope.", result.ErrorMessage);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void Invalid_WithErrors_IsInvalidWithDefaultMessageAndErrors()
    {
        Dictionary<string, string[]> errors = new() { ["Name"] = ["Required."] };

        Result<int> result = Result<int>.Invalid(errors);

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(Result.DefaultValidationMessage, result.ErrorMessage);
        Assert.Same(errors, result.ValidationErrors);
    }

    [Fact]
    public void Error_WithValidationErrors_IsErrorWithDefaultMessageAndErrors()
    {
        Dictionary<string, string[]> errors = new() { ["Name"] = ["Required."] };

        Result<int> result = Result<int>.Error(errors);

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Equal(Result.DefaultValidationMessage, result.ErrorMessage);
        Assert.Same(errors, result.ValidationErrors);
    }

    [Fact]
    public void Error_WithNoValidationErrors_Throws()
    {
        Assert.Throws<ArgumentException>(() => Result.Error(new Dictionary<string, string[]>()));
    }

    [Fact]
    public void Invalid_WithNoErrors_Throws()
    {
        Assert.Throws<ArgumentException>(() => Result.Invalid(new Dictionary<string, string[]>()));
    }

    [Fact]
    public void FromFailed_WithFailure_CopiesEverythingIntoOtherType()
    {
        Result<int> failed = Result<int>.Invalid(
            new Dictionary<string, string[]> { ["Name"] = ["Required."] },
            "Bad.");

        Result<string> copied = Result<string>.FromFailed(failed);

        Assert.Equal(failed.Status, copied.Status);
        Assert.Equal("Bad.", copied.ErrorMessage);
        Assert.Same(failed.ValidationErrors, copied.ValidationErrors);
        Assert.Null(copied.Value);
    }

    [Fact]
    public void FromFailed_WithSuccess_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => Result.FromFailed(Result.Success()));
        Assert.Throws<InvalidOperationException>(
            () => Result<int>.FromFailed(Result<string>.Success("x")));
    }

    [Fact]
    public void ToString_WithFailure_ShowsStatusMessageAndCode()
    {
        Assert.Equal(
            "Conflict: The order is closed. [orders_1001]",
            Result.Conflict(OrderClosed).ToString());
        Assert.Equal("NotFound", Result.NotFound().ToString());
        Assert.Equal("Success: 5", Result<int>.Success(5).ToString());
    }

    private static Result FromCode(ResultStatus status) => status switch
    {
        ResultStatus.Error => Result.Error(OrderClosed),
        ResultStatus.NotFound => Result.NotFound(OrderClosed),
        ResultStatus.Unauthorized => Result.Unauthorized(OrderClosed),
        ResultStatus.Forbidden => Result.Forbidden(OrderClosed),
        ResultStatus.Unavailable => Result.Unavailable(OrderClosed),
        _ => Result.Conflict(OrderClosed),
    };

    private static Result<int> FromCodeTyped(ResultStatus status) => status switch
    {
        ResultStatus.Error => Result<int>.Error(OrderClosed),
        ResultStatus.NotFound => Result<int>.NotFound(OrderClosed),
        ResultStatus.Unauthorized => Result<int>.Unauthorized(OrderClosed),
        ResultStatus.Forbidden => Result<int>.Forbidden(OrderClosed),
        ResultStatus.Unavailable => Result<int>.Unavailable(OrderClosed),
        _ => Result<int>.Conflict(OrderClosed),
    };
}
