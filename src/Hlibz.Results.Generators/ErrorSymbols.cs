using Microsoft.CodeAnalysis;

namespace Hlibz.Results.Generators;

/// <summary>
/// The Hlibz.Results types of one compilation, and the rules the generated manifest and the
/// analyzer share: which types are error types, which unions are error sets.
/// </summary>
internal sealed class ErrorSymbols
{
    /// <summary>The metadata name of <c>Hlibz.Results.ErrorManifestAttribute</c>.</summary>
    public const string ManifestAttributeName = "Hlibz.Results.ErrorManifestAttribute";

    private ErrorSymbols(
        INamedTypeSymbol error,
        INamedTypeSymbol ambientError,
        INamedTypeSymbol errorAttribute,
        INamedTypeSymbol? union,
        INamedTypeSymbol? resultWithErrors)
    {
        Error = error;
        AmbientError = ambientError;
        ErrorAttribute = errorAttribute;
        Union = union;
        ResultWithErrors = resultWithErrors;
    }

    /// <summary>Gets <c>Hlibz.Results.Error</c>.</summary>
    public INamedTypeSymbol Error { get; }

    /// <summary>Gets <c>Hlibz.Results.AmbientError</c>.</summary>
    public INamedTypeSymbol AmbientError { get; }

    /// <summary>Gets <c>Hlibz.Results.ErrorAttribute</c>.</summary>
    public INamedTypeSymbol ErrorAttribute { get; }

    /// <summary>Gets <c>System.Runtime.CompilerServices.IUnion</c>, if the runtime has it.</summary>
    public INamedTypeSymbol? Union { get; }

    /// <summary>Gets <c>Hlibz.Results.Result&lt;T, TError&gt;</c>.</summary>
    public INamedTypeSymbol? ResultWithErrors { get; }

    /// <summary>
    /// Finds the types in <paramref name="compilation"/>.
    /// </summary>
    /// <param name="compilation">The compilation.</param>
    /// <returns>The symbols, or <see langword="null"/> when it doesn't reference Hlibz.Results.</returns>
    public static ErrorSymbols? For(Compilation compilation) =>
        compilation.GetTypeByMetadataName("Hlibz.Results.Error") is { } error
        && compilation.GetTypeByMetadataName("Hlibz.Results.AmbientError") is { } ambient
        && compilation.GetTypeByMetadataName("Hlibz.Results.ErrorAttribute") is { } attribute
            ? new ErrorSymbols(
                error,
                ambient,
                attribute,
                compilation.GetTypeByMetadataName("System.Runtime.CompilerServices.IUnion"),
                compilation.GetTypeByMetadataName("Hlibz.Results.Result`2"))
            : null;

    /// <summary>
    /// Gets whether code at the assembly's top level can name <paramref name="type"/>: not
    /// generic, and not in a private or protected nested type.
    /// </summary>
    /// <param name="type">A type.</param>
    /// <param name="compilation">The compilation.</param>
    /// <returns><see langword="true"/> when generated code can name it.</returns>
    public static bool CanName(INamedTypeSymbol type, Compilation compilation)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType)
            {
                return false;
            }
        }

        return compilation.IsSymbolAccessibleWithin(type, compilation.Assembly);
    }

    /// <summary>
    /// Gets whether <paramref name="type"/> is <c>Error</c> or derives from it.
    /// </summary>
    /// <param name="type">A type.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool IsError(ITypeSymbol? type) => DerivesFrom(type, Error);

    /// <summary>
    /// Gets whether <paramref name="type"/> is an <c>AmbientError</c>.
    /// </summary>
    /// <param name="type">A type.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool IsAmbient(ITypeSymbol? type) => DerivesFrom(type, AmbientError);

    /// <summary>
    /// Gets whether <paramref name="type"/> is an error type that needs describing: a
    /// non-abstract class deriving from <c>Error</c>.
    /// </summary>
    /// <param name="type">A type.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool IsConcreteError(INamedTypeSymbol type) =>
        type is { TypeKind: TypeKind.Class, IsAbstract: false } && IsError(type);

    /// <summary>
    /// The type's own <c>[Error]</c>, or <see langword="null"/>.
    /// </summary>
    /// <param name="type">An error type.</param>
    /// <returns>The attribute.</returns>
    public AttributeData? ErrorAttributeOf(INamedTypeSymbol type) =>
        type.GetAttributes().FirstOrDefault(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, ErrorAttribute));

    /// <summary>
    /// Gets whether an error type carries data of its own: a public instance property declared
    /// between it and <c>Error</c>.
    /// </summary>
    /// <param name="type">An error type.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public bool HasDetails(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type;
             current is not null && !SymbolEqualityComparer.Default.Equals(current, Error);
             current = current.BaseType)
        {
            if (current.GetMembers().OfType<IPropertySymbol>().Any(property =>
                    property is { IsStatic: false, IsIndexer: false, DeclaredAccessibility: Accessibility.Public }))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The case types of <paramref name="type"/> when it is a union (a struct implementing
    /// <c>IUnion</c>, as a <c>union</c> declaration compiles to): its public constructors' single
    /// parameter types, in order; otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="type">A type.</param>
    /// <returns>The case types.</returns>
    public IReadOnlyList<ITypeSymbol>? CasesOf(INamedTypeSymbol type)
    {
        if (Union is null
            || type.TypeKind != TypeKind.Struct
            || !type.AllInterfaces.Contains(Union, SymbolEqualityComparer.Default))
        {
            return null;
        }

        return
        [
            .. type.InstanceConstructors
                .Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public
                                      && constructor.Parameters.Length == 1)
                .Select(constructor => constructor.Parameters[0].Type),
        ];
    }

    /// <summary>
    /// The error types of <paramref name="type"/> when it is an error set: a non-generic union
    /// whose cases are all non-ambient, non-abstract error types; otherwise <see langword="null"/>.
    /// </summary>
    /// <param name="type">A type.</param>
    /// <returns>The error types.</returns>
    public IReadOnlyList<INamedTypeSymbol>? ErrorSetCasesOf(INamedTypeSymbol type)
    {
        if (type.IsGenericType || CasesOf(type) is not { Count: > 0 } cases)
        {
            return null;
        }

        List<INamedTypeSymbol> errors = [];
        foreach (ITypeSymbol @case in cases)
        {
            if (@case is not INamedTypeSymbol named || !IsConcreteError(named) || IsAmbient(named))
            {
                return null;
            }

            errors.Add(named);
        }

        return errors;
    }

    /// <summary>
    /// Gets whether <paramref name="type"/> is <c>Result&lt;T, TError&gt;</c>, and its error
    /// set type argument.
    /// </summary>
    /// <param name="type">A type.</param>
    /// <param name="errorSet">Its <c>TError</c>.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public bool IsResultWithErrors(ITypeSymbol? type, out ITypeSymbol errorSet)
    {
        if (type is INamedTypeSymbol { TypeArguments.Length: 2 } named
            && SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, ResultWithErrors))
        {
            errorSet = named.TypeArguments[1];
            return true;
        }

        errorSet = null!;
        return false;
    }

    private static bool DerivesFrom(ITypeSymbol? type, INamedTypeSymbol baseType)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current.OriginalDefinition, baseType))
            {
                return true;
            }
        }

        return false;
    }
}
