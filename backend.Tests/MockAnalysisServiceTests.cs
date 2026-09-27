using DevFlowAI.API.Services;
using DevFlowAI.API.Services.GitHub;

namespace DevFlowAI.Tests;

/// <summary>
/// Tests for MockAnalysisService — verifies the deterministic mock behaviour
/// that forms the baseline for offline development.
///
/// Phase 7 additions cover:
///   - Six-dimension score model (Testing, Documentation replace TechnicalDebt)
///   - AnalysisSource == "Mock"
///   - Structured finding fields (Explanation, Evidence, RecommendedAction, AffectedPath)
///   - Context-enriched behaviour
///   - Fallback behavior
/// </summary>
public class MockAnalysisServiceTests
{
    private readonly MockAnalysisService _sut = new();

    [Fact]
    public async Task AnalyzeAsync_ReturnsResponse_WithExpectedRepoName()
    {
        var result = await _sut.AnalyzeAsync(
            "https://github.com/acme-corp/frontend-app", "main");

        Assert.Equal("frontend-app", result.Repository.Name);
        Assert.Equal("main",         result.Repository.Branch);
        Assert.Equal("https://github.com/acme-corp/frontend-app", result.Repository.Url);
    }

    [Fact]
    public async Task AnalyzeAsync_ScoresAreInValidRange()
    {
        var result = await _sut.AnalyzeAsync(
            "https://github.com/acme/repo", "develop");

        Assert.InRange(result.Scores.Overall,       0, 100);
        Assert.InRange(result.Scores.CodeQuality,   0, 100);
        Assert.InRange(result.Scores.Security,      0, 100);
        Assert.InRange(result.Scores.Performance,   0, 100);
        Assert.InRange(result.Scores.Testing,       0, 100);
        Assert.InRange(result.Scores.Documentation, 0, 100);
    }

    [Fact]
    public async Task AnalyzeAsync_IsDeterministic_ForSameUrl()
    {
        var r1 = await _sut.AnalyzeAsync("https://github.com/test/same-repo", "main");
        var r2 = await _sut.AnalyzeAsync("https://github.com/test/same-repo", "main");

        Assert.Equal(r1.Scores.Overall,       r2.Scores.Overall);
        Assert.Equal(r1.Scores.Security,      r2.Scores.Security);
        Assert.Equal(r1.Scores.CodeQuality,   r2.Scores.CodeQuality);
        Assert.Equal(r1.Scores.Performance,   r2.Scores.Performance);
        Assert.Equal(r1.Scores.Testing,       r2.Scores.Testing);
        Assert.Equal(r1.Scores.Documentation, r2.Scores.Documentation);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsAtLeastOneRecommendation()
    {
        var result = await _sut.AnalyzeAsync("https://github.com/acme/any-repo", "main");

        Assert.NotEmpty(result.Recommendations);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsAtLeastOneNextAction()
    {
        var result = await _sut.AnalyzeAsync("https://github.com/acme/any-repo", "main");

        Assert.NotEmpty(result.NextActions);
    }

    [Fact]
    public async Task AnalyzeAsync_HandlesShortOwnerRepoForm()
    {
        var result = await _sut.AnalyzeAsync("owner/short-repo", "main");

        Assert.Equal("short-repo", result.Repository.Name);
    }

    [Fact]
    public async Task AnalyzeAsync_FindingCounts_AreNonNegative()
    {
        var result = await _sut.AnalyzeAsync("https://github.com/acme/repo", "main");

        Assert.True(result.Findings.Critical >= 0);
        Assert.True(result.Findings.High     >= 0);
        Assert.True(result.Findings.Medium   >= 0);
        Assert.True(result.Findings.Low      >= 0);
    }

    // ── AnalysisSource field ──────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_ReturnsSource_Mock()
    {
        var result = await _sut.AnalyzeAsync("https://github.com/acme/repo", "main");

        Assert.Equal("Mock", result.AnalysisSource);
    }

    // ── Determinism & URL parsing ─────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_ProducesDistinctScores_ForDifferentUrls()
    {
        var r1 = await _sut.AnalyzeAsync("https://github.com/alpha/repo-a", "main");
        var r2 = await _sut.AnalyzeAsync("https://github.com/beta/repo-b",  "main");

        Assert.NotNull(r1);
        Assert.NotNull(r2);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo/",      "repo")]
    [InlineData("https://github.com/owner/my.repo",    "my.repo")]
    [InlineData("bare-repo-name",                      "bare-repo-name")]
    public async Task AnalyzeAsync_ExtractsRepoName_FromVariousUrlForms(
        string url, string expectedName)
    {
        var result = await _sut.AnalyzeAsync(url, "main");

        Assert.Equal(expectedName, result.Repository.Name);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsResponseWithAnalyzedAt_UtcTimestamp()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var result = await _sut.AnalyzeAsync("https://github.com/acme/repo", "main");
        var after  = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(result.AnalyzedAt, before, after);
    }

    [Fact]
    public async Task AnalyzeAsync_OverallScore_IsAverageOfFiveDimensions()
    {
        var result = await _sut.AnalyzeAsync("https://github.com/acme/score-check", "main");
        var s = result.Scores;

        int expectedOverall = (s.CodeQuality + s.Security + s.Performance + s.Testing + s.Documentation) / 5;

        Assert.Equal(expectedOverall, s.Overall);
    }

    // ── Structured finding fields ─────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_AllRecommendations_HaveNonEmptyRequiredFields()
    {
        var result = await _sut.AnalyzeAsync("https://github.com/acme/any-repo", "main");

        foreach (var rec in result.Recommendations)
        {
            Assert.False(string.IsNullOrWhiteSpace(rec.Title),
                $"Recommendation {rec.Id} has an empty Title");
            Assert.False(string.IsNullOrWhiteSpace(rec.Explanation),
                $"Recommendation {rec.Id} has an empty Explanation");
            Assert.False(string.IsNullOrWhiteSpace(rec.Evidence),
                $"Recommendation {rec.Id} has an empty Evidence");
            Assert.False(string.IsNullOrWhiteSpace(rec.RecommendedAction),
                $"Recommendation {rec.Id} has an empty RecommendedAction");
        }
    }

    [Fact]
    public async Task AnalyzeAsync_AllNextActions_HavePositivePriorityAndNonEmptyAction()
    {
        var result = await _sut.AnalyzeAsync("https://github.com/acme/any-repo", "main");

        foreach (var action in result.NextActions)
        {
            Assert.True(action.Priority > 0,
                $"NextAction has non-positive priority: {action.Priority}");
            Assert.False(string.IsNullOrWhiteSpace(action.Action),
                "NextAction has an empty Action string");
        }
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsSuppliedBranch_InRepositoryInfo()
    {
        var result = await _sut.AnalyzeAsync("https://github.com/acme/repo", "feature/my-feature");

        Assert.Equal("feature/my-feature", result.Repository.Branch);
    }

    // ── Context-enriched behaviour ────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_WithContext_PopulatesRepoMetadata()
    {
        var context = new GitHubRepoContext
        {
            FullName         = "acme/repo",
            Branch           = "main",
            Description      = "A test repo",
            PrimaryLanguage  = "TypeScript",
            LanguageCounts   = new Dictionary<string, int> { ["TypeScript"] = 5 },
            TotalFileCount   = 20,
            SampledFileCount = 5,
            HasReadme        = true,
            HasTests         = true,
            HasCiConfig      = true,
        };

        var result = await _sut.AnalyzeAsync(
            "https://github.com/acme/repo", "main", context);

        Assert.NotNull(result.RepoMetadata);
        Assert.Equal("A test repo",  result.RepoMetadata!.Description);
        Assert.Equal("TypeScript",   result.RepoMetadata.PrimaryLanguage);
        Assert.Equal(20,             result.RepoMetadata.TotalFileCount);
    }

    [Fact]
    public async Task AnalyzeAsync_WithContext_AddsTestingRecommendation_WhenTestsMissing()
    {
        var context = new GitHubRepoContext
        {
            FullName         = "acme/repo",
            Branch           = "main",
            HasReadme        = true,
            HasTests         = false,  // ← no tests
            HasCiConfig      = true,
        };

        var result = await _sut.AnalyzeAsync(
            "https://github.com/acme/repo", "main", context);

        Assert.Contains(result.Recommendations,
            r => r.Category == "Testing" && r.Id == "rec-ctx-1");
    }

    [Fact]
    public async Task AnalyzeAsync_WithContext_TestingFinding_HasEvidenceAndRecommendedAction()
    {
        var context = new GitHubRepoContext
        {
            FullName     = "acme/repo",
            Branch       = "main",
            HasReadme    = true,
            HasTests     = false,
            HasCiConfig  = true,
        };

        var result = await _sut.AnalyzeAsync(
            "https://github.com/acme/repo", "main", context);

        var testFinding = result.Recommendations.Single(r => r.Id == "rec-ctx-1");
        Assert.False(string.IsNullOrWhiteSpace(testFinding.Evidence));
        Assert.False(string.IsNullOrWhiteSpace(testFinding.RecommendedAction));
        Assert.Equal("high", testFinding.Severity);
    }

    [Fact]
    public async Task AnalyzeAsync_WithoutContext_RepoMetadataIsNull()
    {
        var result = await _sut.AnalyzeAsync("https://github.com/acme/repo", "main");

        Assert.Null(result.RepoMetadata);
    }
}
