using Cities.Api;

using Hlibz.Results;
using Hlibz.Results.AspNetCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<CityService>();
builder.Services.AddResultErrorResponses();
builder.Services.AddErrorCodeCatalog(typeof(CityNotFound).Assembly);

WebApplication app = builder.Build();

// Handlers return results as they are: WithResultResponses sends each as its response (the value,
// or an error body with the error's data as details) and documents every declared error.
RouteGroupBuilder cities = app.MapGroup("api/cities").WithResultResponses();
cities.MapGet("{id:guid}", (Guid id, CityService service) => service.Get(id));
cities.MapPost(string.Empty, (CreateCity request, CityService service) => service.Create(request))
    .WithCreatedLocation<City>(city => $"/api/cities/{city.Id}");
cities.MapDelete("{id:guid}", (Guid id, CityService service) => service.Delete(id));

// A handler that sends something else (here a CSV file) returns an IResult, which declares no
// errors: ProducesErrorSet documents the ones it sends with ToHttpResult().
cities.MapGet("{id:guid}/export", IResult (Guid id, CityService service) =>
    {
        Result<City, GetCityError> result = service.Get(id);
        return result is { IsFailure: false, Value: { } city }
            ? TypedResults.Text($"id,name\n{city.Id},{city.Name}\n", "text/csv")
            : result.ToHttpResult();
    })
    .ProducesErrorSet<GetCityError>();

// Every code the API can return, with its status and the schema of its details.
app.MapErrorCodes();

await app.RunAsync();
