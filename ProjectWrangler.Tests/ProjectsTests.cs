using ProjectWrangler.GitHub;

namespace ProjectWrangler.Tests;

public class ProjectsTests
{
    [Fact]
    public void IsIssueUrl_ValidUrl_ReturnsExpectedResult()
    {
        // Arrange
        var url = "https://github.COM/github/edge-foundation/issues/100";

        // Act
        var result = Projects.IsIssueUrl(url);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(url.ToLowerInvariant(), result?.Url);
        Assert.Equal("github", result?.Owner);
        Assert.Equal("edge-foundation", result?.Repository);
        Assert.Equal(100, result?.Number);
    }

    [Fact]
    public void IsIssueUrl_InvalidUrl_ReturnsNull()
    {
        // Arrange
        var url = "https://github.com/github/edge-foundation/pull/100";

        // Act
        var result = Projects.IsIssueUrl(url);

        // Assert
        Assert.Null(result);
    }
}