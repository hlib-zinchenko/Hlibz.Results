using System.Reflection;

namespace Hlibz.Results;

/// <summary>
/// Collects every <see cref="Error"/> type declared in the given assemblies, plus the
/// <see cref="AmbientError"/>s, so the codes an API can return are listed in one place without
/// registering them by hand.
/// </summary>
/// <remarks>
/// Nothing is scanned at runtime: the package's source generator lists each assembly's error
/// types when it compiles (and reports two that share a code), and the catalog reads that list,
/// so it is trimming-safe. Two assemblies sharing a code are found when the catalog is first used.
/// An assembly that references Hlibz.Results but was compiled without the generator throws.
/// </remarks>
public sealed class ErrorCodeCatalog : IErrorCodeCatalog
{
    private readonly Lazy<IReadOnlyList<ErrorDescriptor>> _all;

    /// <summary>
    /// Initializes a new instance of the <see cref="ErrorCodeCatalog"/> class. The assemblies are
    /// read on first use of <see cref="All"/>; call <see cref="Validate"/> to do it at startup.
    /// </summary>
    /// <param name="assemblies">The assemblies declaring the error types.</param>
    public ErrorCodeCatalog(IEnumerable<Assembly> assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        Assembly[] read = [typeof(Error).Assembly, .. assemblies];
        _all = new Lazy<IReadOnlyList<ErrorDescriptor>>(() => Read(read.Distinct()));
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Two error types share a code.</exception>
    public IReadOnlyList<ErrorDescriptor> All => _all.Value;

    /// <summary>
    /// Reads the assemblies now, so duplicate codes fail at startup rather than on first use.
    /// </summary>
    /// <exception cref="InvalidOperationException">Two error types share a code.</exception>
    public void Validate() => _ = All;

    private static List<ErrorDescriptor> Read(IEnumerable<Assembly> assemblies)
    {
        List<ErrorDescriptor> errors = [.. assemblies.SelectMany(ErrorsIn)];
        string[] duplicateCodes =
        [
            .. errors
                .GroupBy(error => error.Code, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key} ({string.Join(", ", group.Select(error => error.Type.FullName))})"),
        ];

        if (duplicateCodes.Length > 0)
        {
            throw new InvalidOperationException(
                $"Duplicate error code(s) found: {string.Join("; ", duplicateCodes)}.");
        }

        return [.. errors.OrderBy(error => error.Code, StringComparer.Ordinal)];
    }

    /// <summary>
    /// The error types the source generator listed in <paramref name="assembly"/>. None for an
    /// assembly that doesn't reference Hlibz.Results, which can't declare any.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members annotated with 'RequiresUnreferencedCodeAttribute' require dynamic access otherwise can break functionality when trimming application code",
        Justification = "Trimming removes only references nothing uses, so it can't hide one to Hlibz.Results.")]
    private static IEnumerable<ErrorDescriptor> ErrorsIn(Assembly assembly)
    {
        if (assembly.GetCustomAttribute<ErrorManifestAttribute>() is not null)
        {
            return ErrorDescriptor.In(assembly).Values;
        }

        Assembly results = typeof(Error).Assembly;
        bool referencesResults = assembly.GetReferencedAssemblies().Any(name =>
            AssemblyName.ReferenceMatchesDefinition(name, results.GetName()));
        return !referencesResults
            ? []
            : throw new InvalidOperationException(
                $"{assembly.GetName().Name} has no list of error types: the Hlibz.Results source "
                + "generator didn't run when it was compiled. Reference the Hlibz.Results package "
                + "from that project, and don't exclude its analyzers.");
    }
}
