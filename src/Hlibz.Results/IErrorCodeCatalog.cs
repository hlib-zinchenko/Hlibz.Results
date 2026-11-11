namespace Hlibz.Results;

/// <summary>
/// Every error code an application can return, e.g. to publish them to API clients.
/// </summary>
public interface IErrorCodeCatalog
{
    /// <summary>
    /// Gets every error code, ordered by <see cref="ErrorCode.Code"/>.
    /// </summary>
    IReadOnlyList<ErrorCode> All { get; }
}
