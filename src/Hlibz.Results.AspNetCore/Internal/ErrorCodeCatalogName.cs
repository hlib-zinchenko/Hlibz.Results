namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// The name of a catalog <c>AddErrorCodeCatalog(name, ...)</c> registered, one service per call,
/// since keyed services can't be listed.
/// </summary>
/// <param name="Name">The catalog's name, its service key.</param>
internal sealed record ErrorCodeCatalogName(string Name);
