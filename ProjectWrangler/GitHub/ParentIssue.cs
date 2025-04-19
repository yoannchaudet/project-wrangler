namespace ProjectWrangler.GitHub;

/// <summary>
///     A project v2 field option which is representing a parent issue.
/// </summary>
public class ParentIssue
{
    public string FieldId { get; set; }
    public string FieldOptionId { get; set; }

    public Issue Issue { get; set; }

    public string? IssueId { get; set; }
    
    public string ? IssueTitle { get; set; }
}