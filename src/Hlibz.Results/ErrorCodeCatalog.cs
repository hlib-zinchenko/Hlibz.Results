using System.Reflection;

namespace Hlibz.Results;

/// <summary>
/// Collects every <see cref="ErrorCode"/> declared as a public static field or property (readonly
/// or not) on any class in the given assemblies, so the codes an API can return are listed in one
/// place without registering them by hand.
/// </summary>
public sealed class ErrorCodeCatalog : IErrorCodeCatalog
{
    private const BindingFlags MemberFlags = BindingFlags.Public | BindingFlags.Static;

    private readonly Lazy<IReadOnlyList<ErrorCode>> _all;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCodeCatalog"/> class. The assemblies are
    /// scanned on first use of <see cref="All"/>; call <see cref="Validate"/> to do it at startup.
    /// </summary>
    /// <param name="assemblies">The assemblies declaring the error codes.</param>
    public ErrorCodeCatalog(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        Assembly[] scanned = [.. assemblies.Distinct()];
        _all = new Lazy<IReadOnlyList<ErrorCode>>(() => Scan(scanned));
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Two error codes share a code.</exception>
    public IReadOnlyList<ErrorCode> All => _all.Value;

    /// <summary>
    /// Scans the assemblies now, so duplicate codes fail at startup rather than on first use.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two error codes share a code.</exception>
    public void Validate() => _ = All;

    private static List<ErrorCode> Scan(IReadOnlyCollection<Assembly> assemblies)
    {
        List<ErrorCode> errorCodes =
        [
            .. assemblies
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => type.IsClass && !type.ContainsGenericParameters)
                .SelectMany(ReadErrorCodes),
        ];

        // The same instance exposed twice (e.g. a field aliased by a property) is one code; two
        // declarations are two codes, even when they are equal records.
        errorCodes = [.. errorCodes.Distinct(ReferenceEqualityComparer.Instance).Cast<ErrorCode>()];

        string[] duplicateCodes =
        [
            .. errorCodes
                .GroupBy(errorCode => errorCode.Code, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key),
        ];

        if (duplicateCodes.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate error code(s) found: {string.Join(", ", duplicateCodes)}.");
        }

        return [.. errorCodes.OrderBy(errorCode => errorCode.Code, StringComparer.Ordinal)];
    }

    private static IEnumerable<ErrorCode> ReadErrorCodes(Type type)
    {
        IEnumerable<object?> fieldValues = type
            .GetFields(MemberFlags)
            .Where(field => typeof(ErrorCode).IsAssignableFrom(field.FieldType))
            .Select(field => field.GetValue(null));

        IEnumerable<object?> propertyValues = type
            .GetProperties(MemberFlags)
            .Where(property => typeof(ErrorCode).IsAssignableFrom(property.PropertyType)
                               && property.GetIndexParameters().Length == 0
                               && property.GetMethod is not null)
            .Select(property => property.GetValue(null));

        return fieldValues.Concat(propertyValues).OfType<ErrorCode>();
    }
}
