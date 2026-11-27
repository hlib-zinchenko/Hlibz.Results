using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hlibz.Results.Generators;

/// <summary>
/// Suppresses CA1815 ("override equals and operator equals on value types") on error sets. A
/// <c>union</c> compiles to a struct without equality members of its own, so the rule flags every
/// error set under <c>AnalysisMode=All</c>; but an error set answers "which error is it?", and
/// nothing compares two of them. Only unions that are error sets (see
/// <see cref="ErrorSymbols.ErrorSetCasesOf"/>): any other struct keeps the warning.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ErrorSetSuppressor : DiagnosticSuppressor
{
    private const string Justification =
        "An error set is a union of error types: it says which error a result failed with, and is "
        + "never compared for equality.";

    /// <summary>The suppression of CA1815 on an error set.</summary>
    private static readonly SuppressionDescriptor ValueTypeEquality = new(
        id: "HRESSPR1815",
        suppressedDiagnosticId: "CA1815",
        justification: Justification);

    /// <inheritdoc/>
    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions { get; } =
        ImmutableArray.Create(ValueTypeEquality);

    /// <inheritdoc/>
    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        if (ErrorSymbols.For(context.Compilation) is not { } symbols)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.ReportedDiagnostics)
        {
            if (diagnostic.Location.SourceTree is { } tree
                && tree.GetRoot(context.CancellationToken)
                    .FindNode(diagnostic.Location.SourceSpan)
                    .AncestorsAndSelf()
                    .OfType<TypeDeclarationSyntax>()
                    .FirstOrDefault() is { } declaration
                && context.GetSemanticModel(tree).GetDeclaredSymbol(declaration, context.CancellationToken)
                    is INamedTypeSymbol type
                && symbols.ErrorSetCasesOf(type) is not null)
            {
                context.ReportSuppression(Suppression.Create(ValueTypeEquality, diagnostic));
            }
        }
    }
}
