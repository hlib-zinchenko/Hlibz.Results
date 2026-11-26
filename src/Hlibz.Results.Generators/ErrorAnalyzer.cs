using System.Collections.Concurrent;
using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Hlibz.Results.Generators;

/// <summary>
/// Reports what would otherwise fail at runtime: an error type the generated manifest can't list
/// (<c>HRES001</c>, <c>HRES002</c>, <c>HRES006</c>), two error types sharing a code
/// (<c>HRES003</c>), a <c>Result&lt;T, TError&gt;</c> whose <c>TError</c> isn't an error set
/// (<c>HRES004</c>) or an error returned where the set doesn't list it (<c>HRES005</c>).
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ErrorAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostics' category.</summary>
    internal const string Category = "Hlibz.Results";

    /// <summary>Where the diagnostics are explained.</summary>
    internal const string HelpLink = "https://github.com/hlib-zinchenko/Hlibz.Results#build-errors";

    /// <summary>HRES001: an error type generated code can't name.</summary>
    internal static readonly DiagnosticDescriptor Unreachable = new(
        "HRES001",
        "Error type isn't reachable by the generated manifest",
        "{0} can't be listed: the generated manifest can't name a generic error type or one nested in a private or protected type",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>HRES002: an error type without <c>[Error]</c>.</summary>
    internal static readonly DiagnosticDescriptor Undescribed = new(
        "HRES002",
        "Error type has no [Error] attribute",
        "{0} needs an [Error(code, status, message)] attribute of its own",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>HRES003: two error types share a code.</summary>
    internal static readonly DiagnosticDescriptor DuplicateCode = new(
        "HRES003",
        "Error code is used twice",
        "{0} and {1} share the error code '{2}'",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink,
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>HRES004: <c>TError</c> isn't an error set.</summary>
    internal static readonly DiagnosticDescriptor NotAnErrorSet = new(
        "HRES004",
        "Error set isn't a union of error types",
        "{0} isn't an error set: declare it as a non-generic union whose cases are all error types, none of them ambient",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>HRES005: an error the set doesn't list.</summary>
    internal static readonly DiagnosticDescriptor NotInErrorSet = new(
        "HRES005",
        "Error isn't in the result's error set",
        "{0} isn't one of {1}'s errors: add it to the union, or return one of its errors",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <summary>HRES006: an <c>[Error]</c> with a blank code or the Success status.</summary>
    internal static readonly DiagnosticDescriptor InvalidAttribute = new(
        "HRES006",
        "Error attribute is invalid",
        "{0}'s [Error] needs a non-blank code and a failure status, not Success",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        helpLinkUri: HelpLink);

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Unreachable, Undescribed, DuplicateCode, NotAnErrorSet, NotInErrorSet, InvalidAttribute);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (ErrorSymbols.For(start.Compilation) is not { } symbols)
            {
                return;
            }

            ConcurrentDictionary<string, ConcurrentBag<(INamedTypeSymbol Type, Location? Location)>> codes =
                new(StringComparer.Ordinal);
            start.RegisterSymbolAction(
                symbolContext => AnalyzeType(symbolContext, symbols, codes),
                SymbolKind.NamedType);
            start.RegisterOperationAction(
                operationContext => AnalyzeConversion(operationContext, symbols),
                OperationKind.Conversion);
            start.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, symbols),
                OperationKind.Invocation);
            start.RegisterCompilationEndAction(end => ReportDuplicates(end, codes));
        });
    }

    private static void AnalyzeType(
        SymbolAnalysisContext context,
        ErrorSymbols symbols,
        ConcurrentDictionary<string, ConcurrentBag<(INamedTypeSymbol Type, Location? Location)>> codes)
    {
        INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
        if (!symbols.IsConcreteError(type))
        {
            return;
        }

        Location? location = type.Locations.FirstOrDefault(candidate => candidate.IsInSource);
        string name = type.ToDisplayString();
        if (!ErrorSymbols.CanName(type, context.Compilation))
        {
            context.ReportDiagnostic(Diagnostic.Create(Unreachable, location, name));
            return;
        }

        if (symbols.ErrorAttributeOf(type) is not { } attribute)
        {
            context.ReportDiagnostic(Diagnostic.Create(Undescribed, location, name));
            return;
        }

        Location? attributeLocation = attribute.ApplicationSyntaxReference?.GetSyntax(context.CancellationToken).GetLocation()
                                      ?? location;
        if (attribute.ConstructorArguments.Length != 3
            || attribute.ConstructorArguments[0].Value is not string code
            || string.IsNullOrWhiteSpace(code)
            || attribute.ConstructorArguments[1].Value is not int status
            || status == 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(InvalidAttribute, attributeLocation, name));
            return;
        }

        codes.GetOrAdd(code, _ => []).Add((type, attributeLocation));
    }

    private static void ReportDuplicates(
        CompilationAnalysisContext context,
        ConcurrentDictionary<string, ConcurrentBag<(INamedTypeSymbol Type, Location? Location)>> codes)
    {
        foreach (KeyValuePair<string, ConcurrentBag<(INamedTypeSymbol Type, Location? Location)>> code in codes)
        {
            (INamedTypeSymbol Type, Location? Location)[] types =
            [
                .. code.Value.OrderBy(entry => entry.Type.ToDisplayString(), StringComparer.Ordinal),
            ];
            for (int index = 1; index < types.Length; index++)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DuplicateCode,
                    types[index].Location,
                    types[index].Type.ToDisplayString(),
                    types[0].Type.ToDisplayString(),
                    code.Key));
            }
        }
    }

    /// <summary>
    /// <c>return error;</c> (or any conversion) into a <c>Result&lt;T, TError&gt;</c>.
    /// </summary>
    private static void AnalyzeConversion(OperationAnalysisContext context, ErrorSymbols symbols)
    {
        IConversionOperation conversion = (IConversionOperation)context.Operation;
        if (conversion.OperatorMethod is not { } method
            || !symbols.IsResultWithErrors(method.ContainingType, out ITypeSymbol errorSet))
        {
            return;
        }

        if (CheckErrorSet(context, symbols, errorSet, conversion.Syntax.GetLocation()) is { } cases
            && symbols.IsError(method.Parameters[0].Type))
        {
            CheckError(context, symbols, conversion.Operand, errorSet, cases);
        }
    }

    /// <summary>
    /// <c>Result&lt;T, TError&gt;.Success(...)</c> and <c>.Failure(...)</c>.
    /// </summary>
    private static void AnalyzeInvocation(OperationAnalysisContext context, ErrorSymbols symbols)
    {
        IInvocationOperation invocation = (IInvocationOperation)context.Operation;
        IMethodSymbol method = invocation.TargetMethod;
        if (!method.IsStatic
            || !symbols.IsResultWithErrors(method.ContainingType, out ITypeSymbol errorSet))
        {
            return;
        }

        if (CheckErrorSet(context, symbols, errorSet, invocation.Syntax.GetLocation()) is { } cases
            && method.Name == "Failure"
            && invocation.Arguments.Length == 1)
        {
            CheckError(context, symbols, invocation.Arguments[0].Value, errorSet, cases);
        }
    }

    /// <summary>
    /// The set's error types, after reporting <c>HRES004</c> when it isn't one;
    /// <see langword="null"/> when it isn't, or is a type parameter (unknown here).
    /// </summary>
    private static IReadOnlyList<INamedTypeSymbol>? CheckErrorSet(
        OperationAnalysisContext context,
        ErrorSymbols symbols,
        ITypeSymbol errorSet,
        Location location)
    {
        if (errorSet is not INamedTypeSymbol named)
        {
            return null;
        }

        if (symbols.ErrorSetCasesOf(named) is { } cases)
        {
            return cases;
        }

        context.ReportDiagnostic(Diagnostic.Create(NotAnErrorSet, location, named.ToDisplayString()));
        return null;
    }

    /// <summary>
    /// Reports <c>HRES005</c> when <paramref name="operand"/>'s type is a concrete error type that
    /// is neither ambient nor (derived from) one of the set's. An expression typed <c>Error</c> or
    /// another abstract error type can't be told apart here, and is checked when it runs.
    /// </summary>
    private static void CheckError(
        OperationAnalysisContext context,
        ErrorSymbols symbols,
        IOperation operand,
        ITypeSymbol errorSet,
        IReadOnlyList<INamedTypeSymbol> cases)
    {
        while (operand is IConversionOperation { IsImplicit: true, OperatorMethod: null } implicitConversion)
        {
            operand = implicitConversion.Operand;
        }

        if (operand.Type is not INamedTypeSymbol type
            || !symbols.IsConcreteError(type)
            || symbols.IsAmbient(type)
            || cases.Any(@case => IsOrDerivesFrom(type, @case)))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            NotInErrorSet,
            operand.Syntax.GetLocation(),
            type.ToDisplayString(),
            errorSet.ToDisplayString()));
    }

    private static bool IsOrDerivesFrom(ITypeSymbol type, ITypeSymbol baseType)
    {
        for (ITypeSymbol? current = type; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
            {
                return true;
            }
        }

        return false;
    }
}
