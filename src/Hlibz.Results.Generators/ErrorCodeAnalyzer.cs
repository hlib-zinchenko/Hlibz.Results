using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hlibz.Results.Generators;

/// <summary>
/// Warns about an error code the generated catalog list can't name (<c>HRES001</c>): one in a
/// private or protected nested class, which the catalog would otherwise silently leave out.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ErrorCodeAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// HRES001: the error code isn't reachable by the generated list, so no catalog lists it.
    /// </summary>
    internal static readonly DiagnosticDescriptor Unreachable = new(
        "HRES001",
        "Error code isn't reachable by the generated catalog list",
        "{0} is left out of every error code catalog: the generated list can't read it; make the type that declares it, and any type it is nested in, internal or public",
        "Hlibz.Results",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "ErrorCodeCatalog reads the list of error codes the package's source generator writes into each assembly, and generated code can't name a member of a private or protected nested class.",
        helpLinkUri: "https://github.com/hlib-zinchenko/Hlibz.Results#build-warnings");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(Unreachable);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            if (start.Compilation.GetTypeByMetadataName(ErrorCodeSymbols.ErrorCodeName) is not { } errorCode)
            {
                return;
            }

            start.RegisterSymbolAction(
                symbolContext => Analyze(symbolContext, errorCode),
                SymbolKind.Field,
                SymbolKind.Property);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, INamedTypeSymbol errorCode)
    {
        ISymbol member = context.Symbol;
        if (member.IsImplicitlyDeclared
            || !ErrorCodeSymbols.IsErrorCode(member, errorCode)
            || ErrorCodeSymbols.IsReachable(member, context.Compilation))
        {
            return;
        }

        context.ReportDiagnostic(Diagnostic.Create(
            Unreachable,
            member.Locations.FirstOrDefault(location => location.IsInSource),
            $"{member.ContainingType.ToDisplayString()}.{member.Name}"));
    }
}
