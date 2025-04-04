using ActionsMinUtils;

namespace ProjectWrangler;

public class BaseActionContext : ActionContext
{
    // Inputs
    public string ProjectOrg => GetInput("project_org", true)!;
    public int ProjectNumber => int.Parse(GetInput("project_number", true)!);

    public string ProjectFieldName => GetInput("project_field_name", true)!;

    // A personal access token (fine grained) with the following scopes:
    //   - Owner = Organizaation in which the project lives
    //   - All repositories
    // Repo permissions:
    //   - issues = read/write
    // Org permissions:
    //   - Project = readonly
    public string ProjectGitHubToken => GetInput("project_github_token", true)!;
}