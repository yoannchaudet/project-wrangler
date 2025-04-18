using System.Net;
using System.Net.Mime;
using System.Text.Json;
using System.Text.RegularExpressions;
using ActionsMinUtils;
using Octokit.GraphQL;
using Octokit.GraphQL.Core.Deserializers;
using Octokit.GraphQL.Model;
using ProjectWrangler.GitHub.Queries;
using Scriban;
using ProjectIssuesResponse = ProjectWrangler.GitHub.Queries.ProjectIssues.Response;

namespace ProjectWrangler.GitHub;

public class Projects(
    ActionsMinUtils.github.GitHub github)
{
    /// <summary>
    ///     Retrieves the unique identifier of a GitHub issue.
    /// </summary>
    /// <param name="issue">The issue for which to retrieve the ID.</param>
    /// <returns>
    ///     A string representing the issue ID if successful; otherwise, <c>null</c>.
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
    ///     Retrieves parent issues from a GitHub project field based on a specified field name.
    /// </summary>
    /// <param name="org">The organization name.</param>
    /// <param name="projectNumber">The project number.</param>
    /// <param name="fieldName">The name of the field to search for.</param>
    /// <param name="first">The number of fields to fetch per request (default is 20).</param>
    /// <returns>
    ///     A collection of <see cref="ParentIssue" /> objects representing the parent issues.
    /// </returns>
    public async Task<(string?, string?, List<ParentIssue>)> GetParentIssues(string org,
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
                                field.Id,
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
                                FieldId = field.Id.Value,
                                FieldOptionId = option.Id,
                                Issue = issue
                            });
                    }

                    return (field.Id.Value, field.Name, parentIssues);
                }

            // Pass down cursor for pagination
            cursor = results.PageInfo.HasNextPage
                ? results.PageInfo.EndCursor
                : null;
        } while (cursor != null);

        // Fallback
        return
            (null, null, [
            ]);
    }

    public async IAsyncEnumerable<ProjectIssue> GetProjectIssues(string org, int projectNumber, string fieldName)
    {
        // Prep query
        var queryTemplate = Template.Parse(QueryUtils.GetProjectIssuesQuery());
        var uri = new Uri("graphql", UriKind.Relative);

        string? cursor = null;
        do
        {
            // Render the query
            var query = await queryTemplate.RenderAsync(new
            {
                org,
                projectNumber,
                first = 100,
                fieldName,
                cursor
            });

            // Execute the query
            var apiResponse = await github.RestClient.Connection.Post<string>(uri, new { Query = query },
                MediaTypeNames.Application.Json, MediaTypeNames.Application.Json);
            if (apiResponse.HttpResponse.StatusCode != HttpStatusCode.OK)
                throw new Exception($"Unable to get project issues: {apiResponse.HttpResponse.StatusCode}");
            var response =
                JsonSerializer.Deserialize<ProjectIssuesResponse>(apiResponse.HttpResponse.Body.ToString()!)!;
            if (response.Data.Organization == null)
                throw new Exception($"Organization not found: {org}");

            foreach (var node in response.Data.Organization.ProjectV2.Items.Nodes)
            {
                // Ignore anything but issues and issues without a matching field
                if (node.Type != "ISSUE" || node.FieldValueByName == null)
                    continue;

                yield return new ProjectIssue(node.Content.Id, node.Content.Title, node.Content.Parent?.Id,
                    node.FieldValueByName!.OptionId);
            }

            // Get next cursor
            if (response.Data.Organization.ProjectV2.Items.PageInfo.HasNextPage)
                cursor = response.Data.Organization.ProjectV2.Items.PageInfo.EndCursor;
            else
                cursor = null;
        } while (cursor != null);
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