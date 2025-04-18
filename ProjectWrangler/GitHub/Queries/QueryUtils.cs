namespace ProjectWrangler.GitHub.Queries;

/// <summary>
///     Utility class for working with GraphQL queries stored as embedded resources.
/// </summary>
public static class QueryUtils
{
    /// <summary>
    ///     Gets the content of the ProjectIssues GraphQL query.
    /// </summary>
    /// <returns>The GraphQL query as a string.</returns>
    public static string GetProjectIssuesQuery()
    {
        return GetEmbeddedResourceContent("ProjectIssues.Query.graphql");
    }

    public static string GetAddSubIssueMutation()
    {
        return GetEmbeddedResourceContent("AddSubIssue.Mutation.graphql");
    }

    /// <summary>
    ///     Gets the content of an embedded resource from the Queries namespace.
    /// </summary>
    /// <param name="resourceName">The name of the resource file.</param>
    /// <returns>The content of the embedded resource as a string.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource cannot be found.</exception>
    private static string GetEmbeddedResourceContent(string resourceName)
    {
        var type = typeof(QueryUtils);
        var fullResourceName = $"{type.Namespace}.{resourceName}";

        using var stream = type.Assembly.GetManifestResourceStream(fullResourceName)!;
        if (stream == null) throw new InvalidOperationException($"Resource '{fullResourceName}' not found.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}