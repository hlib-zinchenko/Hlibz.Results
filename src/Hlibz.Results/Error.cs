using System.Text;
using System.Text.Json.Serialization;

namespace Hlibz.Results;

/// <summary>
/// A failure, as a type: derive a <see langword="sealed"/> record per failure, mark it with
/// <see cref="ErrorAttribute"/> (its code, status and message), and give it the data a client
/// needs to act on it, which goes out with the error. Return it where a result is expected: it
/// converts to a failed <see cref="Result"/>.
/// </summary>
/// <remarks>
/// The package's source generator lists every error type of an assembly when it compiles, so
/// <see cref="ErrorCodeCatalog"/> and the HTTP documentation know them without scanning, and
/// reports one it can't describe as a build error.
/// </remarks>
/// <example>
/// <code>
/// [Error("cities_1002", ResultStatus.Conflict, "The city is used by user profiles.")]
/// public sealed record CityInUse(Guid CityId, int UserCount) : Error;
/// </code>
/// </example>
public abstract record Error
{
    /// <summary>
    /// Gets the machine-readable code clients branch on, from the type's
    /// <see cref="ErrorAttribute"/>.
    /// </summary>
    [JsonIgnore]
    public string Code => Descriptor.Code;

    /// <summary>
    /// Gets what failed, from the type's <see cref="ErrorAttribute"/>; the HTTP layer maps it to
    /// a status code.
    /// </summary>
    [JsonIgnore]
    public ResultStatus Status => Descriptor.Status;

    /// <summary>
    /// Gets a human-readable description of the failure: the type's
    /// <see cref="ErrorAttribute.Message"/>, as <see cref="FormatMessage"/> words it.
    /// </summary>
    [JsonIgnore]
    public string Message => FormatMessage(Descriptor.Message);

    /// <summary>
    /// Gets the description of this error's type.
    /// </summary>
    [JsonIgnore]
    public ErrorDescriptor Descriptor => ErrorDescriptor.For(GetType());

    /// <summary>
    /// Words this error's message; override it to mention the error's data. Protected rather than
    /// a virtual <see cref="Message"/>, so the message never goes out as part of the details.
    /// </summary>
    /// <param name="message">The type's <see cref="ErrorAttribute.Message"/>.</param>
    /// <returns>The message.</returns>
    protected virtual string FormatMessage(string message) => message;

    /// <summary>
    /// Writes the error's members for <see cref="object.ToString"/>: its code, then (in a derived
    /// record) its own data.
    /// </summary>
    /// <param name="builder">Where to write them.</param>
    /// <returns><see langword="true"/>: there is always a member.</returns>
    protected virtual bool PrintMembers(StringBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Append("Code = ").Append(Code);
        return true;
    }
}
