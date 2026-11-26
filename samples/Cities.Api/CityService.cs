using System.Collections.Concurrent;

using Hlibz.Results;

namespace Cities.Api;

public sealed record City(Guid Id, string Name);

public sealed record CreateCity(string Name);

/// <summary>
/// The use cases. Each returns the value or one of its declared errors (or an ambient one, such
/// as <see cref="ValidationFailed"/>); none of them decides a status code.
/// </summary>
public sealed class CityService
{
    /// <summary>How many of the blocking users a <see cref="CityInUse"/> lists.</summary>
    private const int ListedUsers = 50;

    private readonly ConcurrentDictionary<Guid, City> _cities = new();

    private readonly ConcurrentDictionary<Guid, Guid> _userCities = new();

    public CityService()
    {
        City lviv = new(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Lviv");
        City kyiv = new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Kyiv");
        _cities[lviv.Id] = lviv;
        _cities[kyiv.Id] = kyiv;
        _userCities[Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")] = lviv.Id;
        _userCities[Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")] = lviv.Id;
    }

    public Result<City, GetCityError> Get(Guid id) =>
        _cities.TryGetValue(id, out City? city) ? city : new CityNotFound(id);

    public Result<City, CreateCityError> Create(CreateCity request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            // An ambient error: any operation may fail validation, so no set declares it.
            return new ValidationFailed(new Dictionary<string, string[]>
            {
                [nameof(CreateCity.Name)] = ["A city needs a name."],
            });
        }

        if (_cities.Values.Any(city => string.Equals(city.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return new CityNameTaken(request.Name);
        }

        City created = new(Guid.NewGuid(), request.Name);
        _cities[created.Id] = created;
        return created;
    }

    public Result<Unit, DeleteCityError> Delete(Guid id)
    {
        // An error of a smaller set passes on into a larger one that lists it too.
        Result<City, GetCityError> found = Get(id);
        if (found.IsFailure)
        {
            return found.Error;
        }

        Guid[] users = [.. _userCities.Where(pair => pair.Value == id).Select(pair => pair.Key)];
        if (users.Length > 0)
        {
            return new CityInUse(id, users.Length, [.. users.Take(ListedUsers)]);
        }

        _cities.TryRemove(id, out _);
        return Unit.Value;
    }
}
