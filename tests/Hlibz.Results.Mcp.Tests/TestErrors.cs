#pragma warning disable SA1402, SA1649 // The test errors, together.

namespace Hlibz.Results.Mcp.Tests;

[Error("city_not_found", ResultStatus.NotFound, "The city doesn't exist.")]
public sealed record CityNotFound(Guid CityId) : Error;

[Error("city_in_use", ResultStatus.Conflict, "Users live in the city.")]
public sealed record CityInUse(Guid CityId, int UserCount) : Error;

[Error("city_closed", ResultStatus.Error, "The city is closed.")]
public sealed record CityClosed : Error;
