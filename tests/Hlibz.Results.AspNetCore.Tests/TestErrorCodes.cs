namespace Hlibz.Results.AspNetCore.Tests;

public static class TestErrorCodes
{
    public static readonly ErrorCode OrderClosed = new TestErrorCode("1001", "The order is closed.");

    public static readonly ErrorCode Duplicate = new TestErrorCode("1002", "Already exists.");
}

public sealed record TestErrorCode(string Code, string Message) : ErrorCode($"test_{Code}", Message);

public sealed record OrderDto(int OrderId, string CustomerName);
