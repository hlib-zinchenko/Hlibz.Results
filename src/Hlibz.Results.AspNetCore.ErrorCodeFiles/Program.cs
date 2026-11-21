using System.Reflection;
using System.Runtime.Loader;

namespace Hlibz.Results.AspNetCore.Tool;

/// <summary>
/// Writes an app's error code catalogs to JSON files at build time. Run by
/// Hlibz.Results.AspNetCore's buildTransitive target inside the app
/// (<c>dotnet exec --depsfile app.deps.json --runtimeconfig app.runtimeconfig.json</c>), with:
/// <c>--assembly</c> the app's assembly, <c>--output</c> the directory to write to and
/// <c>--file-list</c> the file that lists the written files for the target's incremental check;
/// optionally <c>--environment</c>, the app's environment (<c>Production</c> unless the build's own
/// environment variables say otherwise), as ASP.NET Core's build-time OpenAPI documents take it.
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

            IServiceProvider services = AppServices.Capture(
                entryPoint,
                AppArguments(options),
                HostTimeout);
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

    /// <summary>
    /// The arguments the app's entry point gets: <c>--environment</c> when the build asks for one,
    /// also set as <c>ASPNETCORE_ENVIRONMENT</c> and <c>DOTNET_ENVIRONMENT</c> (this process
    /// only) for an app that doesn't hand its arguments to its builder.
    /// </summary>
    private static string[] AppArguments(Dictionary<string, string> options)
    {
        if (!options.TryGetValue("--environment", out string? environment))
        {
            return [];
        }

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", environment);
        Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", environment);
        return ["--environment", environment];
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        string[] required = ["--assembly", "--output", "--file-list"];
        string[] known = [.. required, "--environment"];
        Dictionary<string, string> options = new(StringComparer.Ordinal);
        for (int index = 0; index < args.Length; index += 2)
        {
            if (!known.Contains(args[index], StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"Unknown option {args[index]}; the options are {string.Join(", ", known)}.",
                    nameof(args));
            }

            options[args[index]] = index + 1 < args.Length
                ? args[index + 1]
                : throw new ArgumentException($"{args[index]} needs a value.", nameof(args));
        }

        string[] missing = [.. required.Where(name => !options.ContainsKey(name))];
        return missing.Length == 0
            ? options
            : throw new ArgumentException($"Missing {string.Join(", ", missing)}.", nameof(args));
    }
}
