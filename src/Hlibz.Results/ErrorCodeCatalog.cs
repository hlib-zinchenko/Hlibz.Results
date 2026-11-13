using System.Reflection;

namespace Hlibz.Results;

/// <summary>
/// Collects every <see cref="ErrorCode"/> declared as a public static field or property (readonly
/// or not) on any class in the given assemblies, so the codes an API can return are listed in one
/// place without registering them by hand.
/// </summary>
/// <remarks>
/// Nothing is scanned at runtime: the package's source generator lists each assembly's error
/// codes when it compiles, and the catalog reads that list, so it is trimming-safe. An assembly
/// that references Hlibz.Results but was compiled without the generator throws when scanned.
/// </remarks>
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
                .SelectMany(ErrorCodesIn),
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

    /// <summary>
    /// The error codes the source generator listed in <paramref name="assembly"/>. None for an
    /// assembly that doesn't reference Hlibz.Results (or is Hlibz.Results), which can't declare
    /// any; a dynamic one (Reflection.Emit), which no generator ran on, is scanned instead.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "Trimming removes only references nothing uses, and dynamic assemblies don't exist in a trimmed app.")]
    private static IEnumerable<ErrorCode> ErrorCodesIn(Assembly assembly)
    {
        if (assembly.GetCustomAttribute<ErrorCodeManifestAttribute>() is { } manifest)
        {
            return manifest.ErrorCodes.Select(read => read()).OfType<ErrorCode>();
        }

        if (assembly.IsDynamic)
        {
            return assembly.GetTypes()
                .Where(type => type.IsClass && !type.ContainsGenericParameters)
                .SelectMany(ReadErrorCodes);
        }

        Assembly results = typeof(ErrorCode).Assembly;
        bool referencesResults = assembly.GetReferencedAssemblies().Any(name =>
            AssemblyName.ReferenceMatchesDefinition(name, results.GetName()));
        return assembly == results || !referencesResults
            ? []
            : throw new InvalidOperationException(
                $"{assembly.GetName().Name} has no list of error codes: the Hlibz.Results source "
                + "generator didn't run when it was compiled. Reference the Hlibz.Results package "
                + "from that project, and don't exclude its analyzers.");
    }

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method",
        Justification = "Only for dynamic assemblies, which don't exist in a trimmed app.")]
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
