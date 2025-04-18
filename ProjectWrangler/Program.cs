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
    // - get all options for the field we track, extract parent issues from options with a matching description
    // - lookup all issue ids based on url
    // - query all issues on the project board, extract parent issue (if any) and field value we track (if any)
    // - for each issue, if the field value is not empty, check if the parent issue is the expected one, if not, update it

    // Get parent issues for the project field by looking at description on options when they look like an issue URL
    Logger.Info(
        $"Looking for parent issues for field {baseContext.ProjectFieldName} in project {baseContext.ProjectOrg}/{baseContext.ProjectNumber}...");
    var (fieldName, parentIssues) = await projects.GetParentIssues(
        baseContext.ProjectOrg,
        baseContext.ProjectNumber,
        baseContext.ProjectFieldName
    );
    if (fieldName == null)
    {
        Logger.Warning("Unable to find field name for project field {baseContext.ProjectFieldName}");
        return;
    }

    if (!fieldName.Equals(baseContext.ProjectFieldName))
    {
        Logger.Info($"Retrieved case-corrected field name: {fieldName}");
    }

    Logger.Info($"Found {parentIssues.Count} parent issue(s)");

    // Find all parent issue ids too
    Logger.Info($"Querying global id for parent parent issue(s)...");
    {
        var tasks = new List<Task<string?>>();
        foreach (var parentIssue in parentIssues)
            tasks.Add(projects.GetIssueId(parentIssue.Issue));
        await Task.WhenAll(tasks);
        for (var i = 0; i < tasks.Count; i++)
            parentIssues[i].IssueId = tasks[i].Result;
    }

    Logger.Info($"Iterating over project issues...");
    await foreach (var projectIssue in projects.GetProjectIssues(baseContext.ProjectOrg, baseContext.ProjectNumber,
                       fieldName!))
    {
        Console.WriteLine(
            $"Processing issue {projectIssue.Id} field=({projectIssue.FieldOptionId}), parent=({projectIssue.ParentId})");
    }
}
finally
{
    sw.Stop();
    Logger.Info($"🏁 Processing done in {sw.Elapsed:g}");
}