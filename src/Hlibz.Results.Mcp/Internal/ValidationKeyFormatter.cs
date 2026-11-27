namespace Hlibz.Results.Mcp.Internal;

/// <summary>
/// Converts a property path such as <c>Items[0].UnitPrice</c> segment by segment, keeping the
/// dots and indexers: <c>items[0].unitPrice</c> under camelCase. The same as
/// Hlibz.Results.AspNetCore's, which this package doesn't reference.
/// </summary>
internal static class ValidationKeyFormatter
{
    public static string Format(string key, JsonNamingPolicy? policy)
    {
        if (policy is null || key.Length == 0)
        {
            return key;
        }

        string[] segments = key.Split('.');
        for (int index = 0; index < segments.Length; index++)
        {
            string segment = segments[index];
            int indexer = segment.IndexOf('[', StringComparison.Ordinal);
            string name = indexer < 0 ? segment : segment[..indexer];
            string rest = indexer < 0 ? string.Empty : segment[indexer..];
            segments[index] = (name.Length == 0 ? name : policy.ConvertName(name)) + rest;
        }

        return string.Join('.', segments);
    }
}
