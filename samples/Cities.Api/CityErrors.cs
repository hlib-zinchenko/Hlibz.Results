using Hlibz.Results;

namespace Cities.Api;

// Each failure is a record carrying what a client needs to act on it. [Error] gives its code,
// what failed (the HTTP layer maps it to a status code) and its message.
[Error("cities_1001", ResultStatus.NotFound, "There's no city with this id.")]
public sealed record CityNotFound(Guid CityId) : Error;

/// <summary>
/// A city still used by user profiles can't be deleted. The error lists them, so the client can
/// show who blocks the delete without asking again.
/// </summary>
[Error("cities_1002", ResultStatus.Conflict, "The city is used by user profiles.")]
public sealed record CityInUse(Guid CityId, int UserCount, IReadOnlyList<Guid> UserIds) : Error
{
    protected override string FormatMessage(string message) =>
        $"The city is used by {UserCount} user profile(s).";
}

[Error("cities_1003", ResultStatus.Conflict, "A city with this name already exists.")]
public sealed record CityNameTaken(string Name) : Error;
