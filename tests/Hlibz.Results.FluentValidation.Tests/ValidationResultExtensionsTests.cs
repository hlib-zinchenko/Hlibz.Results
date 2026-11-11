using FluentValidation.Results;

namespace Hlibz.Results.FluentValidation.Tests;

public sealed class ValidationResultExtensionsTests
{
    private static readonly ValidationResult Failed = new(
    [
        new ValidationFailure("Items[0].Quantity", "Must be positive."),
        new ValidationFailure("Name", "Required."),
        new ValidationFailure("Items[0].Quantity", "Too many."),
    ]);

    [Fact]
    public void ToErrors_WithFailures_GroupsMessagesByRawPropertyPath()
    {
        IReadOnlyDictionary<string, string[]> errors = Failed.ToErrors();

        Assert.Equal(["Must be positive.", "Too many."], errors["Items[0].Quantity"]);
        Assert.Equal(["Required."], errors["Name"]);
    }

    [Fact]
    public void ToErrors_WithValidResult_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ValidationResult().ToErrors());
    }

    [Fact]
    public void ToErrorResult_WithFailures_IsErrorCarryingValidationErrors()
    {
        Result result = Failed.ToErrorResult();
        Result<int> typed = Failed.ToErrorResult<int>();

        Assert.Equal(ResultStatus.Error, result.Status);
        Assert.Equal(ResultStatus.Error, typed.Status);
        Assert.Equal(Result.DefaultValidationMessage, result.ErrorMessage);
        Assert.Equal(2, result.ValidationErrors!.Count);
        Assert.Equal(2, typed.ValidationErrors!.Count);
    }

    [Fact]
    public void ToInvalidResult_WithFailures_IsInvalidCarryingValidationErrors()
    {
        Result result = Failed.ToInvalidResult();
        Result<int> typed = Failed.ToInvalidResult<int>();

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal(ResultStatus.Invalid, typed.Status);
        Assert.Equal(Result.DefaultValidationMessage, result.ErrorMessage);
        Assert.Equal(2, result.ValidationErrors!.Count);
        Assert.Equal(2, typed.ValidationErrors!.Count);
    }

    [Fact]
    public void ToInvalidResult_WithValidResult_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ValidationResult().ToInvalidResult());
    }
}
