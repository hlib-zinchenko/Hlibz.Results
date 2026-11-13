using Microsoft.CodeAnalysis;

namespace Hlibz.Results.Generators;

/// <summary>
/// Picks the members <c>ErrorCodeCatalog</c> collects, the way it did by reflection: public
/// static fields and readable, non-indexed public static properties typed <c>ErrorCode</c> (or a
/// type deriving from it), declared on a class that isn't generic or nested in a generic type.
/// </summary>
internal static class ErrorCodeSymbols
{
    /// <summary>The metadata name of <c>Hlibz.Results.ErrorCode</c>.</summary>
    public const string ErrorCodeName = "Hlibz.Results.ErrorCode";

    /// <summary>
    /// Gets whether the catalog collects <paramref name="member"/>.
    /// </summary>
    /// <param name="member">A field or property.</param>
    /// <param name="errorCode">The <c>ErrorCode</c> symbol.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public static bool IsErrorCode(ISymbol member, INamedTypeSymbol errorCode)
    {
        ITypeSymbol? type = member switch
        {
            IFieldSymbol field => field.Type,
            IPropertySymbol { IsIndexer: false, GetMethod: not null } property => property.Type,
            _ => null,
        };
        if (type is null
            || !member.IsStatic
            || member.DeclaredAccessibility != Accessibility.Public
            || member.ContainingType is not { TypeKind: TypeKind.Class } containingType
            || IsInGenericType(containingType))
        {
            return false;
        }

        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, errorCode))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets whether code at the assembly's top level can read <paramref name="member"/>: not when
    /// it sits in a private or protected nested type, or its getter is private.
    /// </summary>
    /// <param name="member">An error code member.</param>
    /// <param name="compilation">The compilation.</param>
    /// <returns><see langword="true"/> when the generated list can name it.</returns>
    public static bool IsReachable(ISymbol member, Compilation compilation) =>
        compilation.IsSymbolAccessibleWithin(member, compilation.Assembly)
        && (member is not IPropertySymbol { GetMethod: { } getter }
            || compilation.IsSymbolAccessibleWithin(getter, compilation.Assembly));

    private static bool IsInGenericType(INamedTypeSymbol type)
    {
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            if (current.IsGenericType)
            {
                return true;
            }
        }

        return false;
    }
}
