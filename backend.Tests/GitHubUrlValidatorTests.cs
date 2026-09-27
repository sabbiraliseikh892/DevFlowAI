using DevFlowAI.API.Services.GitHub;

namespace DevFlowAI.Tests;

/// <summary>
/// Tests for <see cref="GitHubUrlValidator.TryParse"/>.
/// Covers valid GitHub HTTPS URLs, invalid schemes, non-GitHub hosts,
/// short paths, and edge cases.
/// </summary>
public class GitHubUrlValidatorTests
{
    // ── Valid URLs ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://github.com/owner/repo",              "owner", "repo")]
    [InlineData("https://github.com/owner/repo/",             "owner", "repo")]
    [InlineData("https://github.com/owner/repo.git",          "owner", "repo")]
    [InlineData("https://github.com/owner/repo/tree/main",    "owner", "repo")]
    [InlineData("https://www.github.com/owner/repo",          "owner", "repo")]
    [InlineData("https://github.com/acme-corp/my.project",    "acme-corp", "my.project")]
    public void TryParse_ReturnsTrue_AndPopulatesOwnerRepo_ForValidUrls(
        string url, string expectedOwner, string expectedRepo)
    {
        var ok = GitHubUrlValidator.TryParse(url, out var owner, out var repo);

        Assert.True(ok);
        Assert.Equal(expectedOwner, owner);
        Assert.Equal(expectedRepo,  repo);
    }

    // ── Invalid scheme ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("http://github.com/owner/repo")]    // HTTP, not HTTPS
    [InlineData("git@github.com:owner/repo.git")]   // SSH form
    [InlineData("ssh://github.com/owner/repo")]
    public void TryParse_ReturnsFalse_ForNonHttpsUrls(string url)
    {
        Assert.False(GitHubUrlValidator.TryParse(url, out _, out _));
    }

    // ── Non-GitHub hosts ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("https://bitbucket.org/owner/repo")]
    [InlineData("https://example.com/owner/repo")]
    [InlineData("https://github.evil.com/owner/repo")]
    public void TryParse_ReturnsFalse_ForNonGitHubHosts(string url)
    {
        Assert.False(GitHubUrlValidator.TryParse(url, out _, out _));
    }

    // ── Paths too short ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("https://github.com/")]
    [InlineData("https://github.com")]
    [InlineData("https://github.com/owner")]
    public void TryParse_ReturnsFalse_WhenPathHasFewerThanTwoSegments(string url)
    {
        Assert.False(GitHubUrlValidator.TryParse(url, out _, out _));
    }

    // ── Null / whitespace / relative ─────────────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("owner/repo")]            // relative, not absolute
    [InlineData("not a url at all")]
    public void TryParse_ReturnsFalse_ForNullEmptyOrRelativeInput(string? url)
    {
        Assert.False(GitHubUrlValidator.TryParse(url, out _, out _));
    }

    // ── Normalise ─────────────────────────────────────────────────────────────

    [Fact]
    public void Normalise_ReturnsCanonicalUrl()
    {
        Assert.Equal(
            "https://github.com/owner/repo",
            GitHubUrlValidator.Normalise("owner", "repo"));
    }
}
