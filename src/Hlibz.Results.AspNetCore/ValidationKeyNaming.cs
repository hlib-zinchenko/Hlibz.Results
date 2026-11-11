namespace Hlibz.Results.AspNetCore;

/// <summary>
/// How the keys of <see cref="Result.ValidationErrors"/> (property paths such as
/// <c>Items[0].UnitPrice</c>) are written in a response.
/// </summary>
public enum ValidationKeyNaming
{
    /// <summary>
    /// Each path segment is converted with the app's JSON property naming policy, so keys match
    /// the request's JSON (<c>items[0].unit_price</c> under snake_case). The default.
    /// </summary>
    JsonNamingPolicy = 0,

    /// <summary>
    /// Keys are written exactly as the result holds them.
    /// </summary>
    AsIs,
}
