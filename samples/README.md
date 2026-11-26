# Samples

`Cities.Api`: one minimal API over a few cities, two of which user profiles use.

| File | What it shows |
|---|---|
| `CityErrors.cs` | Errors as records with data (`CityInUse` lists the users that block a delete), and an error wording its message from its data (`FormatMessage`). |
| `CityErrorSets.cs` | Error sets: the errors each operation declares, as unions. |
| `CityService.cs` | `Result<T, TError>` and `Result<Unit, TError>`, an ambient `ValidationFailed` no set declares, and an error of a smaller set passed on into a larger one. |
| `Program.cs` | `WithResultResponses()` on a group (each result sent as is, each declared error documented), `WithCreatedLocation` for 201 + `Location`, and `MapErrorCodes()`. |
| `error-codes/error-codes.json` | The catalog, written by every build (`ErrorCodesGenerateFiles`): each code, its status and its details' schema. Committed; CI fails when a build changes it. |

```bash
dotnet run --project samples/Cities.Api    # http://localhost:5180
```

`Cities.Api.http` has every case. The one the typed errors are for:

```http
DELETE http://localhost:5180/api/cities/11111111-1111-1111-1111-111111111111
```

```json
HTTP/1.1 409 Conflict

{
  "message": "The city is used by 2 user profile(s).",
  "code": "cities_1002",
  "details": {
    "cityId": "11111111-1111-1111-1111-111111111111",
    "userCount": 2,
    "userIds": ["bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"]
  }
}
```

The client knows who blocks the delete without another request.
