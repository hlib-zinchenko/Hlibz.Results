using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hlibz.Results.AspNetCore.Tests;

#pragma warning disable SA1649, SA1402 // The errors and DTOs the tests share, together.

[JsonConverter(typeof(JsonStringEnumConverter<CustomerTier>))]
public enum CustomerTier
{
    Basic,
    Gold,
}

/// <summary>A strongly-typed ID, written as its <see cref="Guid"/> by its own converter.</summary>
[JsonConverter(typeof(CustomerIdConverter))]
public readonly record struct CustomerId(Guid Value);

public static class TestErrors
{
    public static Result Invalid(Dictionary<string, string[]> errors) => new ValidationFailed(errors);

    public static Result<T> Invalid<T>(Dictionary<string, string[]> errors) => new ValidationFailed(errors);
}

[Error("test_1001", ResultStatus.Error, "The order is closed.")]
public sealed record OrderClosed : Error;

[Error("test_1002", ResultStatus.Conflict, "Already exists.")]
public sealed record Duplicate : Error;

[Error("test_1003", ResultStatus.NotFound, "Order 7 was not found.")]
public sealed record OrderNotFound(int OrderId) : Error;

[Error("test_1004", ResultStatus.Conflict, "Already sent.")]
public sealed record AlreadySent : Error;

[Error("test_1005", ResultStatus.NotFound, "No count.")]
public sealed record NoCount : Error;

[Error("test_1006", ResultStatus.Conflict, "Taken.")]
public sealed record Taken(IReadOnlyList<int> TakenBy) : Error;

[Error("test_1007", ResultStatus.NotFound, "No such customer.")]
public sealed record CustomerNotFound(
    CustomerId CustomerId,
    CustomerId? MergedInto,
    IReadOnlyList<CustomerId> Related,
    CustomerTier Tier,
    CustomerTier? PreviousTier) : Error;

public sealed class CustomerIdConverter : JsonConverter<CustomerId>
{
    public override CustomerId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, CustomerId value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Value);
}

public sealed record OrderDto(int OrderId, string CustomerName);
