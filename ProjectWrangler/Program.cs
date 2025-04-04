using ActionsMinUtils;
using ActionsMinUtils.github;
using ProjectWrangler;
using ProjectWrangler.GitHub;

// Get context 
var baseContext = ActionContext.TryCreate<BaseActionContext>()!;

// Init various clients
var github = new GitHub(baseContext.ProjectGitHubToken);
var projects = new Projects(github);

// Core logic
// - get all options for the field we track, extract parent issues from options wiht a matching description
// - lookup all issue ids based on url
// - find all issues on the project board, linked to a parent issue, and if needed, add them as sub issue

var parentIssues = await projects.GetParentIssues(
    baseContext.ProjectOrg,
    baseContext.ProjectNumber,
    baseContext.ProjectFieldName,
    20
);