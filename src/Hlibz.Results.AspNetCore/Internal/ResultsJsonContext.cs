using System.Text.Json.Serialization;

namespace Hlibz.Results.AspNetCore.Internal;

/// <summary>
/// System.Text.Json metadata for the bodies this package writes itself, generated at compile
/// time so they serialize under Native AOT, where reflection-based serialization is off.
/// <c>AddResultErrorResponses</c> appends it to the app's JSON options, after the app's own
/// resolvers, which still win. The app's naming policy applies, as to any type: the property
/// names here are only the C# ones. <see cref="ResultEnvelope{T}"/> and an app's own envelope or
/// value types are the app's to list in its own context.
/// </summary>
[JsonSerializable(typeof(ErrorResponse))]
[JsonSerializable(typeof(ResultEnvelope))]
[JsonSerializable(typeof(IReadOnlyList<ErrorCode>))]
internal sealed partial class ResultsJsonContext : JsonSerializerContext;
