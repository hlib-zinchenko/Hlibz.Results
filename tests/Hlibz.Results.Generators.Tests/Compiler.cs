using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Hlibz.Results.Generators.Tests;

/// <summary>
/// Compiles sources in memory against the test runtime's assemblies, and runs the analyzer or
/// the generator on them.
/// </summary>
internal static class Compiler
{
    private const string Usings = """
        using System;
        using System.Collections.Generic;
        using Hlibz.Results;

        """;

    private static readonly MetadataReference[] AllReferences =
    [
        .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path)),
    ];

    /// <summary>
    /// Compiles <paramref name="source"/> after the shared usings.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="referenceLibrary">Whether to reference Hlibz.Results.</param>
    /// <returns>The compilation, nothing run on it yet.</returns>
    /// <param name="assemblyName">The assembly's name.</param>
    public static CSharpCompilation Compile(string source, bool referenceLibrary = true, string assemblyName = "Generated.Errors-1")
    {
        MetadataReference[] references = referenceLibrary
            ? AllReferences
            : AllReferences
                .Where(reference => !reference.Display!.EndsWith(
                    "Hlibz.Results.dll",
                    StringComparison.Ordinal))
                .ToArray();
        return CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(Usings + source, new CSharpParseOptions(LanguageVersion.Latest), path: "Source.cs")],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    /// <summary>
    /// Runs <see cref="ErrorAnalyzer"/> on <paramref name="source"/>, failing on any
    /// compile error so a test can't pass on a source that doesn't compile.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>What the analyzer reported.</returns>
    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source)
    {
        CSharpCompilation compilation = Compile(source);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(IsError));
        return await compilation
            .WithAnalyzers([new ErrorAnalyzer()])
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Runs <see cref="ErrorSetSuppressor"/> on <paramref name="source"/> behind an analyzer that
    /// reports CA1815 on every struct, as the .NET analyzers do on a struct without equality
    /// members, failing on any compile error.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="referenceLibrary">Whether to reference Hlibz.Results.</param>
    /// <returns>Each CA1815, by the type's name, and whether it was suppressed.</returns>
    public static async Task<IReadOnlyDictionary<string, bool>> SuppressAsync(string source, bool referenceLibrary = true)
    {
        CSharpCompilation compilation = Compile(source, referenceLibrary);
        Assert.Empty(compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(IsError));
        ImmutableArray<Diagnostic> diagnostics = await compilation
            .WithAnalyzers(
                [new ValueTypeEqualityAnalyzer(), new ErrorSetSuppressor()],
                new CompilationWithAnalyzersOptions(
                    new AnalyzerOptions([]),
                    onAnalyzerException: null,
                    concurrentAnalysis: true,
                    logAnalyzerExecutionTime: false,
                    reportSuppressedDiagnostics: true))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        return diagnostics
            .Where(diagnostic => diagnostic.Id == ValueTypeEqualityAnalyzer.Id)
            .ToDictionary(
                diagnostic => diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture),
                diagnostic => diagnostic.IsSuppressed,
                StringComparer.Ordinal);
    }

    /// <summary>
    /// Runs <see cref="ErrorManifestGenerator"/> on <paramref name="source"/>.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="referenceLibrary">Whether to reference Hlibz.Results.</param>
    /// <param name="assemblyName">The assembly's name.</param>
    /// <returns>The generated manifest (or <see langword="null"/>) and the compilation with it.</returns>
    public static (string? Manifest, Compilation Output) Generate(
        string source,
        bool referenceLibrary = true,
        string assemblyName = "Generated.Errors-1")
    {
        GeneratorDriver driver = CSharpGeneratorDriver
            .Create(new ErrorManifestGenerator())
            .RunGeneratorsAndUpdateCompilation(
                Compile(source, referenceLibrary, assemblyName),
                out Compilation output,
                out ImmutableArray<Diagnostic> diagnostics);
        Assert.Empty(diagnostics);

        string? manifest = driver.GetRunResult().GeneratedTrees
            .Select(tree => tree.ToString())
            .SingleOrDefault();
        return (manifest, output);
    }

    /// <summary>
    /// Emits <paramref name="output"/> and loads it into a context of its own, failing on any
    /// compile error.
    /// </summary>
    /// <param name="output">The compilation.</param>
    /// <returns>The loaded assembly.</returns>
    public static Assembly Load(Compilation output)
    {
        using MemoryStream stream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = output.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics.Where(IsError)));

        stream.Position = 0;
        return new AssemblyLoadContext(name: null, isCollectible: true).LoadFromStream(stream);
    }

    private static bool IsError(Diagnostic diagnostic) =>
        diagnostic.Severity == DiagnosticSeverity.Error;

    /// <summary>
    /// Stands in for the .NET analyzers' CA1815: reports every struct, its message the struct's
    /// name.
    /// </summary>
    // A test stand-in, never loaded by a compiler: it reuses CA1815's id on purpose.
#pragma warning disable RS1029, RS1036, RS1041, RS2008
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    private sealed class ValueTypeEqualityAnalyzer : DiagnosticAnalyzer
    {
        public const string Id = "CA1815";

        private static readonly DiagnosticDescriptor Rule = new(
            Id,
            "Override equals and operator equals on value types",
            "{0}",
            "Performance",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(
                symbolContext =>
                {
                    if (symbolContext.Symbol is INamedTypeSymbol { TypeKind: TypeKind.Struct } type)
                    {
                        symbolContext.ReportDiagnostic(Diagnostic.Create(Rule, type.Locations[0], type.Name));
                    }
                },
                SymbolKind.NamedType);
        }
    }
#pragma warning restore RS1029, RS1036, RS1041, RS2008
}
