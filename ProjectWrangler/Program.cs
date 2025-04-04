using System.Diagnostics;
using ActionsMinUtils;
using ActionsMinUtils.github;
using ProjectWrangler;
using ProjectWrangler.GitHub;

// Get context 
var baseContext = ActionContext.TryCreate<BaseActionContext>()!;

// Init various clients
var github = new GitHub(baseContext.ProjectGitHubToken);
var projects = new Projects(github);

// Start a clock
var sw = new Stopwatch();
sw.Start();
try
{
    // Core logic
    // - get all options for the field we track, extract parent issues from options wiht a matching description
    // - lookup all issue ids based on url
    // - find all issues on the project board, linked to a parent issue, and if needed, add them as sub issue

    // Get parent issues for the project field by looking at description on options when they look like an issue URL
    var parentIssues = (await projects.GetParentIssues(
        baseContext.ProjectOrg,
        baseContext.ProjectNumber,
        baseContext.ProjectFieldName
    )).ToList();
    Logger.Info($"Found {parentIssues.Count} parent issue(s)");
    
    // Find all parent issue ids too
    {
        var tasks = new List<Task<string?>>();
        foreach (var parentIssue in parentIssues)
           tasks.Add(projects.GetIssueId(parentIssue.Issue));
        await Task.WhenAll(tasks);
        for (var i = 0; i < tasks.Count; i++)
            parentIssues[i].IssueId = tasks[i].Result;
    }
}
finally
{
    sw.Stop();
    Logger.Info($"🏁 Processing done in {sw.Elapsed:g}");
}