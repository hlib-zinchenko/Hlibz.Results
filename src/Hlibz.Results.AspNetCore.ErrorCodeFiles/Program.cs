using System.Reflection;
using System.Runtime.Loader;

namespace Hlibz.Results.AspNetCore.Tool;

/// <summary>
/// Writes an app's error code catalogs to JSON files at build time. Run by
/// Hlibz.Results.AspNetCore's buildTransitive target inside the app
/// (<c>dotnet exec --depsfile app.deps.json --runtimeconfig app.runtimeconfig.json</c>), with:
/// <c>--assembly</c> the app's assembly, <c>--output</c> the directory to write to and
/// <c>--file-list</c> the file that lists the written files for the target's incremental check.
/// </summary>
internal static class Program
{
    /// <summary>
    /// How long the app gets to build its host, as ASP.NET Core's build-time OpenAPI documents
    /// allow.
    /// </summary>
    private static readonly TimeSpan HostTimeout = TimeSpan.FromMinutes(5);

    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Any failure is reported to the build as an error.")]
    private static int Main(string[] args)
    {
        try
        {
            Dictionary<string, string> options = Parse(args);
            Assembly app = AssemblyLoadContext.Default.LoadFromAssemblyName(
                AssemblyName.GetAssemblyName(options["--assembly"]));
            MethodInfo entryPoint = app.EntryPoint
                                    ?? throw new InvalidOperationException(
                                        $"{app.GetName().Name} has no entry point.");

            IServiceProvider services = AppServices.Capture(entryPoint, HostTimeout);
            IReadOnlyList<string> files = ErrorCodeFilesWriter.Write(services, options["--output"]);

            File.WriteAllLines(options["--file-list"], files);
            foreach (string file in files)
            {
                Console.WriteLine($"{app.GetName().Name} -> {file}");
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Couldn't write the error code files: {exception.Message}");
            Console.WriteLine(exception);
            return 1;
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        string[] required = ["--assembly", "--output", "--file-list"];
        Dictionary<string, string> options = new(StringComparer.Ordinal);
        for (int index = 0; index + 1 < args.Length; index += 2)
        {
            options[args[index]] = args[index + 1];
        }

        string[] missing = [.. required.Where(name => !options.ContainsKey(name))];
        return missing.Length == 0
            ? options
            : throw new ArgumentException($"Missing {string.Join(", ", missing)}.", nameof(args));
    }
}
