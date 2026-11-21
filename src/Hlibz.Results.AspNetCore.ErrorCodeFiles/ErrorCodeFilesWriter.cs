using System.Reflection;

namespace Hlibz.Results.AspNetCore.Tool;

/// <summary>
/// Calls the writer in the app's own copy of Hlibz.Results.AspNetCore
/// (<c>Internal/ErrorCodeFiles.Write</c>), found by name: this tool ships inside that package, so
/// the copy is always the tool's own version, and compiling against it would make the package's
/// project depend on the tool it packs.
/// </summary>
internal static class ErrorCodeFilesWriter
{
    public const string WriterTypeName =
        "Hlibz.Results.AspNetCore.Internal.ErrorCodeFiles, Hlibz.Results.AspNetCore";

    public const string WriterMethodName = "Write";

    /// <summary>
    /// Writes each catalog registered in <paramref name="services"/> into
    /// <paramref name="directory"/>.
    /// </summary>
    /// <param name="services">The app's services.</param>
    /// <param name="directory">The directory to write to.</param>
    /// <returns>The full path of each file.</returns>
    public static IReadOnlyList<string> Write(IServiceProvider services, string directory)
    {
        Type writer = Type.GetType(WriterTypeName)
                      ?? throw new InvalidOperationException(
                          "The app doesn't reference Hlibz.Results.AspNetCore.");
        MethodInfo write = writer.GetMethod(
                               WriterMethodName,
                               BindingFlags.Public | BindingFlags.Static,
                               [typeof(IServiceProvider), typeof(string)])
                           ?? throw new InvalidOperationException(
                               $"{writer.Assembly.GetName()} has no {WriterMethodName} method "
                               + "this tool can call: the tool and the package don't match.");
        return (IReadOnlyList<string>)write.Invoke(
            null,
            BindingFlags.DoNotWrapExceptions,
            null,
            [services, directory],
            null)!;
    }
}
