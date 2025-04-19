using System.Diagnostics;
using ActionsMinUtils;
using ActionsMinUtils.github;
using Octokit.GraphQL.Model;
using ProjectWrangler;
using ProjectWrangler.GitHub;
using ProjectWrangler.GitHub.Queries.ProjectIssues;

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
        var tasks = new List<Task<(string?, string)?>>();
        foreach (var parentIssue in parentIssues)
            tasks.Add(projects.GetIssueId(parentIssue.Issue));
        await Task.WhenAll(tasks);
        for (var i = 0; i < tasks.Count; i++)
        {
            parentIssues[i].IssueId = tasks[i].Result!.Value.Item1;
            parentIssues[i].IssueTitle = tasks[i].Result!.Value.Item2;
        }
    }

    // Build some mappings
    var optionToParentIssue = parentIssues.ToDictionary(
        parentIssue => parentIssue.FieldOptionId,
        parentIssue => parentIssue
    );

    // Build a clean reparenting structure (for console output)
    var reparentingOperations = 0;
    var reparentingStructure = new Dictionary<ParentIssue, List<ProjectIssue>>();

    // Iterate over all project issues (with the field we track, defined)
    Logger.Info("Enumerating project issues to build reparenting structure...");
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
            continue;
        }

        // Build the reparenting structure
        if (!reparentingStructure.ContainsKey(parentIssue))
            reparentingStructure.Add(parentIssue, new List<ProjectIssue>());
        reparentingStructure[parentIssue].Add(projectIssue);
        reparentingOperations++;
    }

    // Print the reparenting structure
    foreach (var parentIssue in reparentingStructure.Keys)
    {
        Logger.Info($"🗂️ {parentIssue.IssueTitle} ({parentIssue.IssueId})");
        foreach (var projectIssue in reparentingStructure[parentIssue])
        {
            Logger.Info($"    📦 {projectIssue.Title} ({projectIssue.Id})");
        }
    }

    // Reparent issues
    if (reparentingStructure.Count > 0)
    {
        Logger.Info($"Executing {reparentingOperations} reparenting operation(s)...");
        var tasks = new List<Task<bool>>();
        foreach (var parentIssue in reparentingStructure.Keys)
        foreach (var projectIssue in reparentingStructure[parentIssue])
        {
           tasks.Add(SafeAddSubIssue(projects, parentIssue, projectIssue));
        }

        await Task.WhenAll(tasks);

        // Summary
        var successCount = tasks.Count(t => t.Result);
        var failureCount = tasks.Count - successCount;
        Logger.Info($"Reparenting complete: {successCount} successful, {failureCount} failed");
    }
    else
    {
        Logger.Info("No reparenting operations to execute");
    }
}
finally
{
    sw.Stop();
    Logger.Info($"🏁 Processing done in {sw.Elapsed:g}");
}

/// <summary>
/// Safely adds a sub-issue and continues execution even if the operation fails
/// </summary>
/// <returns>True if operation succeeded, false otherwise</returns>
static async Task<bool> SafeAddSubIssue(Projects projects, ParentIssue parentIssue, ProjectIssue projectIssue)
{
    try
    {
        await projects.AddSubIssue(parentIssue.IssueId!,projectIssue.Id);
        return true;
    }
    catch (Exception ex)
    {
        Logger.Warning(
            $"Failed to reparent issue {projectIssue.Title} ({projectIssue.Id}) under {parentIssue.IssueTitle} ({parentIssue.IssueId}): {ex.Message}");
        return false;
    }
}