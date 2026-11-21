using System.Text.Json.Serialization.Metadata;

using Microsoft.Extensions.Options;

using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// Writes each registered error code catalog to a JSON file holding the list <c>MapErrorCodes</c>
/// serves, so clients get the codes at build time, without running the API: the default catalog
/// to <c>error-codes.json</c>, a named one to <c>error-codes.{name}.json</c>.
/// </summary>
/// <remarks>
/// Called through reflection by the Hlibz.Results.AspNetCore.ErrorCodeFiles tool, once it has the
/// app's services; its <c>ErrorCodeFilesWriter</c> names this type and <see cref="Write"/>, so keep
/// them in step.
/// </remarks>
internal static class ErrorCodeFiles
{
    public const string DefaultFileName = "error-codes.json";

    /// <summary>
    /// Characters a catalog name can't hold, as it becomes part of a file name: every one invalid
    /// on Windows, so the same names work on every OS.
    /// </summary>
    private const string InvalidNameCharacters = "<>:\"/\\|?*";

    /// <summary>
    /// The type <c>MapErrorCodes</c> serializes. A field, not <c>GetTypeInfo&lt;T&gt;()</c>,
    /// which is .NET 11 only.
    /// </summary>
    private static readonly Type CatalogType = typeof(IReadOnlyList<ErrorCode>);

    /// <summary>
    /// Writes a file per catalog into <paramref name="directory"/>, leaving a file whose content
    /// hasn't changed untouched.
    /// </summary>
    /// <param name="services">The app's services, with its catalogs and JSON options.</param>
    /// <param name="directory">The directory to write to, created if missing.</param>
    /// <returns>The full path of each catalog's file.</returns>
    /// <exception cref="InvalidOperationException">No catalog is registered, or a catalog's
    /// name can't be part of a file name.</exception>
    public static IReadOnlyList<string> Write(IServiceProvider services, string directory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        List<(string FileName, IErrorCodeCatalog Catalog)> catalogs = [];
        if (services.GetService<IErrorCodeCatalog>() is { } defaultCatalog)
        {
            catalogs.Add((DefaultFileName, defaultCatalog));
        }

        foreach (string name in services
                     .GetServices<ErrorCodeCatalogName>()
                     .Select(catalog => catalog.Name)
                     .Distinct(StringComparer.Ordinal))
        {
            catalogs.Add((FileNameFor(name), services.GetRequiredKeyedService<IErrorCodeCatalog>(name)));
        }

        if (catalogs.Count == 0)
        {
            throw new InvalidOperationException(
                "The app registers no error code catalog to write; call AddErrorCodeCatalog.");
        }

        // The JSON options MapErrorCodes' endpoint writes with, indented to read and diff, with
        // the same line ends on every OS so a committed file doesn't change with who built it.
        HttpJsonOptions json = services.GetService<IOptions<HttpJsonOptions>>()?.Value ?? new();
        JsonSerializerOptions options = new(json.SerializerOptions)
        {
            WriteIndented = true,
            NewLine = "\n",
        };
        JsonTypeInfo typeInfo = options.GetTypeInfo(CatalogType);

        Directory.CreateDirectory(directory);
        List<string> files = [];
        foreach ((string fileName, IErrorCodeCatalog catalog) in catalogs)
        {
            string path = Path.GetFullPath(Path.Combine(directory, fileName));
            byte[] content = [.. JsonSerializer.SerializeToUtf8Bytes(catalog.All, typeInfo), (byte)'\n'];

            // An unchanged file keeps its timestamp, so watchers and incremental builds of the
            // clients reading it don't fire on every build of the API.
            if (!File.Exists(path) || !File.ReadAllBytes(path).AsSpan().SequenceEqual(content))
            {
                File.WriteAllBytes(path, content);
            }

            files.Add(path);
        }

        return files;
    }

    private static string FileNameFor(string name) =>
        name.Any(character => char.IsControl(character) || InvalidNameCharacters.Contains(character, StringComparison.Ordinal))
            ? throw new InvalidOperationException(
                $"The error code catalog '{name}' can't be written to a file: its name holds a "
                + $"character a file name can't ({InvalidNameCharacters} or a control character).")
            : $"error-codes.{name}.json";
}
