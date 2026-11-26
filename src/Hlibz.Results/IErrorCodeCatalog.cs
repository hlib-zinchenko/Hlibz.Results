namespace Hlibz.Results;

/// <summary>
/// Every error an application can return, e.g. to publish their codes to API clients.
/// </summary>
public interface IErrorCodeCatalog
{
    /// <summary>
    /// Gets every error, ordered by <see cref="ErrorDescriptor.Code"/>.
    /// </summary>
    IReadOnlyList<ErrorDescriptor> All { get; }
}
