using System.Collections.Immutable;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hlibz.Results.Generators;

/// <summary>
/// Lists the error types (with what their <c>[Error]</c> says) and error sets (unions of error
/// types) of the assembly being compiled in a generated <c>ErrorManifestAttribute</c> applied to
/// the assembly, which <c>ErrorDescriptor</c>, <c>ErrorSetDescriptor</c> and
/// <c>ErrorCodeCatalog</c> read instead of scanning types at runtime. A type it can't list is
/// left out; <see cref="ErrorAnalyzer"/> reports it.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class ErrorManifestGenerator : IIncrementalGenerator
{
    private const string Namespace = "Hlibz.Results.Generated";

    private const string ErrorSetPrefix = "set:";

    private static string GeneratorName => typeof(ErrorManifestGenerator).Assembly.GetName().Name;

    private static string GeneratorVersion =>
        typeof(ErrorManifestGenerator).Assembly.GetName().Version?.ToString() ?? "0.0.0.0";

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // A union declaration is a TypeDeclarationSyntax too (UnionDeclarationSyntax), so this
        // finds error sets with any Roslyn that compiles them.
        IncrementalValuesProvider<string> entries = context.SyntaxProvider
            .CreateSyntaxProvider(
                static (node, _) => node is TypeDeclarationSyntax,
                static (syntaxContext, cancellationToken) => Entry(syntaxContext, cancellationToken))
            .Where(static entry => entry is not null)
            .Select(static (entry, _) => entry!);

        IncrementalValueProvider<string?> manifestClassName = context.CompilationProvider
            .Select(static (compilation, _) => ClassNameFor(compilation));

        context.RegisterSourceOutput(
            entries.Collect().Combine(manifestClassName),
            static (sourceContext, input) => Emit(sourceContext, input.Left, input.Right));
    }

    /// <summary>
    /// The manifest entry for the type declared at the node: an <c>ErrorDescriptor</c> or (prefixed
    /// <see cref="ErrorSetPrefix"/>) an <c>ErrorSetDescriptor</c> expression, or
    /// <see langword="null"/> when it is neither or the generated code can't name it.
    /// </summary>
    private static string? Entry(GeneratorSyntaxContext context, CancellationToken cancellationToken)
    {
        Compilation compilation = context.SemanticModel.Compilation;
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, cancellationToken) is not INamedTypeSymbol type
            || ErrorSymbols.For(compilation) is not { } symbols
            || !ErrorSymbols.CanName(type, compilation))
        {
            return null;
        }

        if (symbols.IsConcreteError(type))
        {
            return ErrorEntry(type, symbols);
        }

        return symbols.ErrorSetCasesOf(type) is { } cases ? ErrorSetPrefix + ErrorSetEntry(type, cases) : null;
    }

    private static string? ErrorEntry(INamedTypeSymbol type, ErrorSymbols symbols)
    {
        if (symbols.ErrorAttributeOf(type) is not { ConstructorArguments: { Length: 3 } arguments }
            || arguments[0].Value is not string code
            || string.IsNullOrWhiteSpace(code)
            || arguments[1].Value is not int status
            || status == 0
            || arguments[2].Value is not string message)
        {
            return null;
        }

        return $"new global::Hlibz.Results.ErrorDescriptor(typeof({Name(type)}), "
               + $"{Literal(code)}, (global::Hlibz.Results.ResultStatus){status}, {Literal(message)}, "
               + $"{(symbols.HasDetails(type) ? "true" : "false")})";
    }

    private static string ErrorSetEntry(INamedTypeSymbol type, IReadOnlyList<INamedTypeSymbol> cases)
    {
        string name = Name(type);
        StringBuilder entry = new();
        entry.Append($"new global::Hlibz.Results.ErrorSetDescriptor(typeof({name}), new global::System.Type[] {{ ");
        entry.Append(string.Join(", ", cases.Select(@case => $"typeof({Name(@case)})")));
        entry.Append(" }, static error => error switch { ");
        foreach (INamedTypeSymbol @case in cases)
        {
            entry.Append($"{Name(@case)} @case => new {name}(@case), ");
        }

        entry.Append("_ => (object?)null })");
        return entry.ToString();
    }

    private static string Name(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    private static string Literal(string value) =>
        SymbolDisplay.FormatLiteral(value, quote: true);

    /// <summary>
    /// The manifest class's name for the assembly being compiled, unique to it so assemblies that
    /// see each other's internals don't clash, or <see langword="null"/> when the compilation
    /// doesn't reference Hlibz.Results (nothing to generate).
    /// </summary>
    private static string? ClassNameFor(Compilation compilation)
    {
        if (compilation.GetTypeByMetadataName(ErrorSymbols.ManifestAttributeName) is null)
        {
            return null;
        }

        StringBuilder name = new("ErrorManifest_");
        foreach (char character in compilation.AssemblyName ?? "Assembly")
        {
            name.Append(char.IsLetterOrDigit(character) ? character : '_');
        }

        return name.ToString();
    }

    private static void Emit(
        SourceProductionContext context,
        ImmutableArray<string> entries,
        string? className)
    {
        if (className is null)
        {
            return;
        }

        // A partial type is declared more than once; list it once, in a stable order.
        string[] distinct = [.. entries.Distinct(StringComparer.Ordinal).OrderBy(entry => entry, StringComparer.Ordinal)];
        string errors = string.Join(
            ",\n",
            distinct.Where(entry => !entry.StartsWith(ErrorSetPrefix, StringComparison.Ordinal))
                .Select(entry => "            " + entry));
        string sets = string.Join(
            ",\n",
            distinct.Where(entry => entry.StartsWith(ErrorSetPrefix, StringComparison.Ordinal))
                .Select(entry => "            " + entry.Substring(ErrorSetPrefix.Length)));

        context.AddSource(
            "ErrorManifest.g.cs",
            $$"""
            // <auto-generated/>
            #nullable enable
            // Obsolete error types are still listed.
            #pragma warning disable CS0612, CS0618

            [assembly: global::{{Namespace}}.{{className}}]

            namespace {{Namespace}}
            {
                /// <summary>
                /// The error types and error sets the Hlibz.Results source generator found in this
                /// assembly.
                /// </summary>
                [global::System.CodeDom.Compiler.GeneratedCode("{{GeneratorName}}", "{{GeneratorVersion}}")]
                internal sealed class {{className}} : global::{{ErrorSymbols.ManifestAttributeName}}
                {
                    private static readonly global::Hlibz.Results.ErrorDescriptor[] AllErrors =
                    {
            {{errors}}
                    };

                    private static readonly global::Hlibz.Results.ErrorSetDescriptor[] AllErrorSets =
                    {
            {{sets}}
                    };

                    /// <inheritdoc/>
                    public override global::System.Collections.Generic.IReadOnlyList<global::Hlibz.Results.ErrorDescriptor> Errors => AllErrors;

                    /// <inheritdoc/>
                    public override global::System.Collections.Generic.IReadOnlyList<global::Hlibz.Results.ErrorSetDescriptor> ErrorSets => AllErrorSets;
                }
            }

            """);
    }
}
