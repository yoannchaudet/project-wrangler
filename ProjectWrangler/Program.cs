using System.Diagnostics;
using ActionsMinUtils;
using ActionsMinUtils.github;
using ProjectWrangler;
using ProjectWrangler.GitHub;

// Get context 
var baseContext = ActionContext.TryCreate<BaseActionContext>()!;

// Initialize clients
var github = new GitHub(baseContext.ProjectGitHubToken);
var projects = new Projects(github);

// Start a clock to measure execution time
var sw = new Stopwatch();
sw.Start();
try
{
    // Core logic:
    // 1. Get all options for the tracked field, extract parent issues from options with matching descriptions
    // 2. Look up all issue IDs based on URLs
    // 3. Query all issues on the project board, extract their parent issues (if any) and tracked field values (if any)
    // 4. For each issue with a non-empty field value, update its parent if it doesn't match the expected one

    if (baseContext.DryRun)
    {
        Logger.Info("Dry run mode, exiting");
        System.Environment.Exit(0);
    }
    
    // Get parent issues for the project field by looking at descriptions on options when they look like an issue URL
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

    // Log the number of parent issues found
    Logger.Info($"Found {parentIssues.Count} parent issue(s)");

    // Fetch IDs for all parent issues
    Logger.Info("Fetching ids for parent issue(s)...");
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

    // Build mapping from field option ID to parent issue
    var optionToParentIssue = parentIssues.ToDictionary(
        parentIssue => parentIssue.FieldOptionId,
        parentIssue => parentIssue
    );

    // Track reparenting operations for console output
    var reparentingOperations = 0;
    var reparentingStructure = new Dictionary<ParentIssue, List<ProjectIssue>>();

    // Iterate over all project issues with the tracked field defined
    Logger.Info("Enumerating project issues to build reparenting structure...");
    await foreach (var projectIssue in projects.GetProjectIssues(baseContext.ProjectOrg, baseContext.ProjectNumber,
                       fieldName!))
    {
        // Ignore issues not matching a field option we track
        if (!optionToParentIssue.TryGetValue(projectIssue.FieldOptionId, out var parentIssue))
            continue;

        // Skip issues that are already properly parented
        if (projectIssue.ParentId != null && projectIssue.ParentId == parentIssue.IssueId)
            continue;

        // Skip issues that would parent themselves
        if (projectIssue.Id == parentIssue.IssueId) continue;

        // Add to the reparenting structure
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
            Logger.Info($"    📦 {projectIssue.Title} ({projectIssue.Id})");
    }

    // Execute reparenting operations
    if (reparentingStructure.Count > 0)
    {
        Logger.Info($"Executing {reparentingOperations} reparenting operation(s)...");
        var tasks = new List<Task<bool>>();
        foreach (var parentIssue in reparentingStructure.Keys)
        foreach (var projectIssue in reparentingStructure[parentIssue])
            tasks.Add(SafeAddSubIssue(projects, parentIssue, projectIssue));

        await Task.WhenAll(tasks);

        // Log summary of results
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
/// <param name="projects">The Projects instance to use for the operation</param>
/// <param name="parentIssue">The parent issue to add the sub-issue to</param>
/// <param name="projectIssue">The issue to be added as a sub-issue</param>
/// <returns>True if operation succeeded, false otherwise</returns>
static async Task<bool> SafeAddSubIssue(Projects projects, ParentIssue parentIssue, ProjectIssue projectIssue)
{
    try
    {
        await projects.AddSubIssue(parentIssue.IssueId!, projectIssue.Id);
        return true;
    }
    catch (Exception ex)
    {
        Logger.Warning(
            $"Failed to reparent issue {projectIssue.Title} ({projectIssue.Id}) under {parentIssue.IssueTitle} ({parentIssue.IssueId}): {ex.Message}");
        return false;
    }
}