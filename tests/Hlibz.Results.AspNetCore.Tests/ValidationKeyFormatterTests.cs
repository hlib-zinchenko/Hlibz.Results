using System.Text.Json;

using Hlibz.Results.AspNetCore.Internal;

namespace Hlibz.Results.AspNetCore.Tests;

public sealed class ValidationKeyFormatterTests
{
    [Theory]
    [InlineData("UnitPrice", "unit_price")]
    [InlineData("Lines[0].UnitPrice", "lines[0].unit_price")]
    [InlineData("Filers[1].IncomeStreams[2].GrossIncome", "filers[1].income_streams[2].gross_income")]
    [InlineData("[0].Name", "[0].name")]
    [InlineData("", "")]
    public void Format_WithSnakeCasePolicy_ConvertsEverySegment(string key, string expected)
    {
        Assert.Equal(expected, ValidationKeyFormatter.Format(key, JsonNamingPolicy.SnakeCaseLower));
    }
}
