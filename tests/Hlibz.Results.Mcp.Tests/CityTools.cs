using System.ComponentModel;

namespace Hlibz.Results.Mcp.Tests;

[McpServerToolType]
public static class CityTools
{
    [McpServerTool(Name = "delete_city")]
    [ToolErrors<DeleteCityError>]
    [ToolError<ValidationFailed>]
    [Description("Deletes a city.")]
    public static string Delete(Guid cityId, string outcome) => Deleting(cityId, outcome).ValueOrThrow();

    [McpServerTool(Name = "close_city")]
    [ToolError<CityClosed>]
    [Description("Closes a city.")]
    public static string Close()
    {
        Result.Failure(new CityClosed()).ThrowIfFailed();
        return "closed";
    }

    [McpServerTool(Name = "ping")]
    [Description("Answers.")]
    public static string Ping() => "pong";

    private static Result<string, DeleteCityError> Deleting(Guid cityId, string outcome) => outcome switch
    {
        "missing" => new CityNotFound(cityId),
        "used" => new CityInUse(cityId, 3),
        "invalid" => new ValidationFailed(new Dictionary<string, string[]>
        {
            ["Items[0].UnitPrice"] = ["Must be positive."],
            ["Name"] = ["Required.", "Too short."],
        }),
        _ => "deleted",
    };
}
