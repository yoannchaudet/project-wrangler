using System.Text.RegularExpressions;
using Octokit.GraphQL;
using Octokit.GraphQL.Model;

namespace ProjectWrangler.GitHub;

public class Projects(ActionsMinUtils.github.GitHub github)
{
    public async Task<string> GetIssueId(string repositoryOwner, string repositoryName, int issueNumber)
    {
        var query = new Query()
            .Repository(repositoryName, repositoryOwner)
            .Issue(issueNumber)
            .SingleOrDefault()
            .Select(issue => issue.Id);
        return "wip";
    }

    public async Task<IEnumerable<ParentIssue>> GetParentIssues(string org, int projectNumber, string fieldName,
        int first = 20)
    {
        string? cursor = null;

        do
        {
            var query = new Query()
                .Organization(org)
                .ProjectV2(projectNumber)
                .Fields(first, cursor)
                .Select(fields => new
                {
                    // Get nodes
                    Nodes = fields.Nodes
                        .OfType<ProjectV2SingleSelectField>()
                        .Select(field =>
                            new
                            {
                                // field name + options
                                field.Name,
                                Options =
                                    field.Options(null).Select(option =>
                                        new { option.Id, option.Description }).ToList()
                            }
                        ).ToList(),

                    // Get page info too (for pagination)
                    PageInfo = new
                    {
                        fields.PageInfo.HasNextPage,
                        fields.PageInfo.EndCursor
                    }
                });

            var results = await github.GraphQLClient.Run(query);
            foreach (var field in results.Nodes)
                // If we found the field we are looking for (matching by name, case insensitive), return its options where descriptions are matching issues
                if (field.Name.ToLowerInvariant().Equals(fieldName.ToLowerInvariant()))
                {
                    var parentIssues = new List<ParentIssue>();
                    foreach (var option in field.Options)
                    {
                        var issue = IsIssueUrl(option.Description);
                        if (issue != null)
                            parentIssues.Add(new ParentIssue { FieldId = option.Id, Issue = issue });
                    }

                    return parentIssues;
                }

            // Pass down cursor for pagination
            cursor = results.PageInfo.HasNextPage ? results.PageInfo.EndCursor : null;

            Console.WriteLine("Cursor: " + cursor);
        } while (cursor != null);

        // Fallback
        return [];
    }

    /// <summary>
    ///     Parses a GitHub issue URL and extracts the repository owner, repository name, and issue number.
    /// </summary>
    /// <param name="url">The GitHub issue URL to parse.</param>
    /// <returns>
    ///     An <see cref="Issue" /> object if the URL is valid; otherwise, <c>null</c>.
    /// </returns>
    public static Issue? IsIssueUrl(string url)
    {
        var regex = new Regex(@"http(s)\://github.com/(?<owner>[^/]+)/(?<repo>[^/]+)/issues/(?<issueId>[0-9]+)");
        url = url.ToLowerInvariant().Trim();
        var match = regex.Match(url);
        if (match.Success)
            return new Issue
            {
                Url = url,
                Owner = match.Groups["owner"].Value,
                Repository = match.Groups["repo"].Value,
                Number = int.Parse(match.Groups["issueId"].Value)
            };
        return null;
    }
}