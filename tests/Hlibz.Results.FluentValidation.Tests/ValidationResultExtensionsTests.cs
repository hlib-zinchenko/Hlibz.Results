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
    public void ToValidationFailed_WithFailures_IsAValidationFailedAnyResultAccepts()
    {
        ValidationFailed error = Failed.ToValidationFailed();
        Result<int> result = error;

        Assert.Equal(ResultStatus.Invalid, result.Status);
        Assert.Equal("validation_failed", result.Error?.Code);
        Assert.Equal(2, error.Errors.Count);
    }

    [Fact]
    public void ToValidationFailed_WithValidResult_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new ValidationResult().ToValidationFailed());
    }
}
