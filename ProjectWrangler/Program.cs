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

    if (true)
    {
        await projects.AddSubIssue("I_kwDOLzx6Fs6ws36v", "I_kwDOLzx6Fs6ybHJf");

        Environment.Exit(1);
    }

    // Get parent issues for the project field by looking at description on options when they look like an issue URL
    Logger.Info(
        $"Looking for parent issues for field {baseContext.ProjectFieldName} in project {baseContext.ProjectOrg}/{baseContext.ProjectNumber}...");
    var (fieldId, fieldName, parentIssues) = await projects.GetParentIssues(
        baseContext.ProjectOrg,
        baseContext.ProjectNumber,
        baseContext.ProjectFieldName
    );

    // Verify we found a matching field name
    if (fieldId == null || fieldName == null)
    {
        Logger.Warning($"Unable to find a field name matching '{baseContext.ProjectFieldName}' (case insensitive)");
        return;
    }

    // Print the case corrected field name (if necessary)
    if (!fieldName.Equals(baseContext.ProjectFieldName))
        Logger.Info($"Field name case-corrected to '{fieldName}'");

    // How many parent issues did we find
    Logger.Info($"Found {parentIssues.Count} parent issue(s)");

    // Find all parent issue ids too
    Logger.Info("Fetching ids for parent parent issue(s)...");
    {
        var tasks = new List<Task<string?>>();
        foreach (var parentIssue in parentIssues)
            tasks.Add(projects.GetIssueId(parentIssue.Issue));
        await Task.WhenAll(tasks);
        for (var i = 0; i < tasks.Count; i++)
            parentIssues[i].IssueId = tasks[i].Result;
    }

    // Build some mappings
    var optionToParentIssue = parentIssues.ToDictionary(
        parentIssue => parentIssue.FieldOptionId,
        parentIssue => parentIssue
    );

    // Iterate over all project issues (with the field we track, defined)
    Logger.Info("Iterating over project issues...");
    await foreach (var projectIssue in projects.GetProjectIssues(baseContext.ProjectOrg, baseContext.ProjectNumber,
                       fieldName!))
    {
        // Ignore issues not matching a field option we track
        if (!optionToParentIssue.TryGetValue(projectIssue.FieldOptionId, out var parentIssue))
            continue;

        // Issue is already properly parented
        if (projectIssue.ParentId != null && projectIssue.ParentId == parentIssue.IssueId)
            continue;

        // Leave issue that would parent-themselves alone
        if (projectIssue.Id == parentIssue.IssueId)
        {
            Console.WriteLine(
                $"IGNORE SELF {projectIssue.Id}: {projectIssue.Title} field=({projectIssue.FieldOptionId}), parent=({projectIssue.ParentId})");
            continue;
        }


        Console.WriteLine(
            $"Need to reparent issue {projectIssue.Id}: {projectIssue.Title} field=({projectIssue.FieldOptionId}), parent=({projectIssue.ParentId})");
    }
}
finally
{
    sw.Stop();
    Logger.Info($"🏁 Processing done in {sw.Elapsed:g}");
}