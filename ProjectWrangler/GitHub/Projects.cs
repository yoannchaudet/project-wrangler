using System.Text.RegularExpressions;
using ActionsMinUtils;
using Octokit.GraphQL;
using Octokit.GraphQL.Core.Deserializers;
using Octokit.GraphQL.Model;

namespace ProjectWrangler.GitHub;

public class Projects(
    ActionsMinUtils.github.GitHub github)
{
    /// <summary>
    /// Retrieves the unique identifier of a GitHub issue.
    /// </summary>
    /// <param name="issue">The issue for which to retrieve the ID.</param>
    /// <returns>
    /// A string representing the issue ID if successful; otherwise, <c>null</c>.
    /// </returns>
    public async Task<string?> GetIssueId(Issue issue)
    {
        var query = new Query()
            .Repository(issue.Repository,
                issue.Owner)
            .Issue(issue.Number)
            .Select(i => i.Id);
        try
        {
            var result = await github.GraphQLClient.Run(query);
            return result.Value;
        }
        catch (ResponseDeserializerException ex)
        {
            Logger.Warning("Unable to get issue id for {issue.Owner}/{issue.Repository}#{issue.Number}: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Retrieves parent issues from a GitHub project field based on a specified field name.
    /// </summary>
    /// <param name="org">The organization name.</param>
    /// <param name="projectNumber">The project number.</param>
    /// <param name="fieldName">The name of the field to search for.</param>
    /// <param name="first">The number of fields to fetch per request (default is 20).</param>
    /// <returns>
    /// A collection of <see cref="ParentIssue"/> objects representing the parent issues.
    /// </returns>
    public async Task<IEnumerable<ParentIssue>> GetParentIssues(string org,
        int projectNumber,
        string fieldName,
        int first = 20)
    {
        string? cursor = null;

        do
        {
            var query = new Query()
                .Organization(org)
                .ProjectV2(projectNumber)
                .Fields(first,
                    cursor)
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
                                    field.Options(null)
                                        .Select(option =>
                                            new
                                            {
                                                option.Id,
                                                option.Description
                                            })
                                        .ToList()
                            }
                        )
                        .ToList(),

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
                if (field.Name.ToLowerInvariant()
                    .Equals(fieldName.ToLowerInvariant()))
                {
                    var parentIssues = new List<ParentIssue>();
                    foreach (var option in field.Options)
                    {
                        var issue = IsIssueUrl(option.Description);
                        if (issue != null)
                            parentIssues.Add(new ParentIssue
                            {
                                FieldId = option.Id,
                                Issue = issue
                            });
                    }

                    return parentIssues;
                }

            // Pass down cursor for pagination
            cursor = results.PageInfo.HasNextPage
                ? results.PageInfo.EndCursor
                : null;

            Console.WriteLine("Cursor: " + cursor);
        } while (cursor != null);

        // Fallback
        return
        [
        ];
    }

    // public async Task GetIssues(string org,
    //     int projectNumber)
    // {
    //     // var query = new Query()
    //     //     .Organization(org)
    //     //     .ProjectV2(number: projectNumber)
    //     //     .Items(first: 100)
    //     //     .Nodes.Select(item => new
    //     //         {
    //     //             item.Id,
    //     //             item.Type,
    //     //             Issue = item.Content.Switch<string>(s => s.Issue(x => x.Title)),
    //     //             Initiative = item.FieldValueByName("Initiative")
    //     //                 .Single().Switch<string>(
    //     //                     s=> s.ProjectV2ItemFieldSingleSelectValue(x => x.Name))
    //     //         }
    //     //     );
    //
    //
    //     var query = new Query()
    //         .Organization("github")
    //         .ProjectV2(13332)
    //         .Items(first:100).Nodes.Select(item => new {
    //             item.Id,
    //             item.Type,
    //             Content = item.Content.Switch<Issue>(
    //                 whenIssue => new {
    //                     whenIssue.Id,
    //                     whenIssue.Number,
    //                     whenIssue.Title
    //                 }
    //             ),
    //             Initiative = item.FieldValueByName("Initiative").Switch<string>(
    //                 (ProjectV2ItemFieldSingleSelectValue singleSelect) => singleSelect.Name,
    //             )
    //         }).ToList();
    //     
    //     Console.WriteLine(query.ToString());
    //     
    //     var results = await github.GraphQLClient.Run(query);
    //     var x = results;
    //
    //     Console.WriteLine("xxx");
    // }

    /// <summary>
    /// Parses a GitHub issue URL and extracts the repository owner, repository name, and issue number.
    /// </summary>
    /// <param name="url">The GitHub issue URL to parse.</param>
    /// <returns>
    /// An <see cref="Issue"/> object if the URL is valid; otherwise, <c>null</c>.
    /// </returns>
    public static Issue? IsIssueUrl(string url)
    {
        var regex = new Regex(@"http(s)\://github.com/(?<owner>[^/]+)/(?<repo>[^/]+)/issues/(?<issueId>[0-9]+)");
        url = url.ToLowerInvariant()
            .Trim();
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