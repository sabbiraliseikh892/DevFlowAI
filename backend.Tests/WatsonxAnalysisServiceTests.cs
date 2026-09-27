using DevFlowAI.API.DTOs;
using DevFlowAI.API.Services;
using DevFlowAI.API.Services.GitHub;
using DevFlowAI.API.Services.Watsonx;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DevFlowAI.Tests;

/// <summary>
/// Tests for WatsonxAnalysisService.
/// The real WatsonxHttpClient is not called — an IWatsonxHttpClient mock is
/// injected so tests run without IBM credentials.
///
/// Phase 7 additions cover:
///   - Valid AI JSON response (new 6-score + structured finding schema)
///   - Malformed AI JSON response
///   - Missing fields in JSON
///   - Invalid severity normalisation
///   - Nonexistent referenced file path validation
///   - Fallback behavior
///   - Score validation (clamp 0-100)
///   - Empty findings list
/// </summary>
public class WatsonxAnalysisServiceTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WatsonxAnalysisService CreateSut(IWatsonxHttpClient client) =>
        new(client, NullLogger<WatsonxAnalysisService>.Instance);

    private static IWatsonxHttpClient MockReturning(string output)
    {
        var mock = new Mock<IWatsonxHttpClient>();
        mock.Setup(c => c.GenerateTextAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(output);
        return mock.Object;
    }

    private static IWatsonxHttpClient MockThrowing(Exception ex)
    {
        var mock = new Mock<IWatsonxHttpClient>();
        mock.Setup(c => c.GenerateTextAsync(
                It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(ex);
        return mock.Object;
    }

    // ── Minimum valid JSON for Phase 7 schema ─────────────────────────────────

    private static string ValidJson(
        int overall = 82, int cq = 85, int sec = 74, int perf = 90,
        int testing = 78, int docs = 80) => $$"""
        {
          "scores": {
            "overall": {{overall}},
            "codeQuality": {{cq}},
            "security": {{sec}},
            "performance": {{perf}},
            "testing": {{testing}},
            "documentation": {{docs}}
          },
          "findings": { "critical": 0, "high": 1, "medium": 2, "low": 4 },
          "recommendations": [
            {
              "id": "rec-1",
              "severity": "high",
              "category": "Security",
              "title": "Outdated dependency",
              "explanation": "lodash < 4.17.21 has a prototype pollution vulnerability.",
              "evidence": "package.json specifies lodash@4.17.15.",
              "recommendedAction": "Upgrade lodash to 4.17.21 or later.",
              "affectedPath": "package.json"
            }
          ],
          "nextActions": [
            { "priority": 1, "action": "Upgrade lodash", "rationale": "Known CVE in current version." }
          ]
        }
        """;

    // ── 1. Valid AI JSON response ─────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_ParsesWellFormedWatsonxJson()
    {
        var sut    = CreateSut(MockReturning(ValidJson()));
        var result = await sut.AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal(82,           result.Scores.Overall);
        Assert.Equal(74,           result.Scores.Security);
        Assert.Equal(78,           result.Scores.Testing);
        Assert.Equal(80,           result.Scores.Documentation);
        Assert.Equal(1,            result.Findings.High);
        Assert.Single(result.Recommendations);
        Assert.Equal("Security",   result.Recommendations[0].Category);
        Assert.Equal("high",       result.Recommendations[0].Severity);
        Assert.Equal("lodash < 4.17.21 has a prototype pollution vulnerability.",
                     result.Recommendations[0].Explanation);
        Assert.Equal("package.json specifies lodash@4.17.15.",
                     result.Recommendations[0].Evidence);
        Assert.Equal("Upgrade lodash to 4.17.21 or later.",
                     result.Recommendations[0].RecommendedAction);
        Assert.Single(result.NextActions);
        Assert.Equal("Upgrade lodash", result.NextActions[0].Action);
        Assert.Equal("app",        result.Repository.Name);
        Assert.Equal("Watsonx",    result.AnalysisSource);
    }

    // ── 2. Malformed AI JSON response → fallback ──────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_ReturnsFallback_WhenModelOutputIsNotJson()
    {
        var sut    = CreateSut(MockReturning("Sorry, I cannot analyse that repository."));
        var result = await sut.AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal(0, result.Scores.Overall);
        Assert.Single(result.Recommendations);
        Assert.Contains("unavailable", result.Recommendations[0].Title,
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Evidence unavailable", result.Recommendations[0].Evidence);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsFallback_WhenJsonIsMalformed()
    {
        var sut    = CreateSut(MockReturning("{scores: broken json"));
        var result = await sut.AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal(0, result.Scores.Overall);
        Assert.Equal("Watsonx", result.AnalysisSource);
    }

    // ── 3. Missing fields in JSON → defaults applied ──────────────────────────

    [Fact]
    public async Task AnalyzeAsync_HandlesPartialJson_WithMissingScores()
    {
        const string partialJson = """
            {
              "findings": { "critical": 0, "high": 1, "medium": 0, "low": 0 },
              "recommendations": [],
              "nextActions": []
            }
            """;

        var result = await CreateSut(MockReturning(partialJson))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        // Missing scores default to 50 (clamped)
        Assert.Equal(50, result.Scores.Overall);
        Assert.Equal(50, result.Scores.CodeQuality);
        Assert.Equal(50, result.Scores.Testing);
        Assert.Equal(50, result.Scores.Documentation);
    }

    [Fact]
    public async Task AnalyzeAsync_SetsEvidenceUnavailable_WhenEvidenceFieldMissing()
    {
        const string json = """
            {
              "scores": {"overall":70,"codeQuality":70,"security":70,"performance":70,"testing":70,"documentation":70},
              "findings": {"critical":0,"high":0,"medium":0,"low":0},
              "recommendations": [
                {
                  "id": "rec-1",
                  "severity": "low",
                  "category": "General",
                  "title": "Missing evidence",
                  "explanation": "Some explanation",
                  "recommendedAction": "Do something"
                }
              ],
              "nextActions": []
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal("Evidence unavailable", result.Recommendations[0].Evidence);
    }

    [Fact]
    public async Task AnalyzeAsync_SetsEvidenceUnavailable_WhenEvidenceFieldIsEmpty()
    {
        const string json = """
            {
              "scores": {"overall":70,"codeQuality":70,"security":70,"performance":70,"testing":70,"documentation":70},
              "findings": {"critical":0,"high":0,"medium":0,"low":0},
              "recommendations": [
                {
                  "id": "rec-1",
                  "severity": "low",
                  "category": "General",
                  "title": "Empty evidence",
                  "explanation": "Explanation",
                  "evidence": "",
                  "recommendedAction": "Act"
                }
              ],
              "nextActions": []
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal("Evidence unavailable", result.Recommendations[0].Evidence);
    }

    // ── 4. Invalid severity → normalised to "medium" ─────────────────────────

    [Fact]
    public async Task AnalyzeAsync_NormalisesUnknownSeverity_ToMedium()
    {
        const string json = """
            {
              "scores": {"overall":70,"codeQuality":70,"security":70,"performance":70,"testing":70,"documentation":70},
              "findings": {"critical":0,"high":0,"medium":0,"low":0},
              "recommendations": [
                {"id": "rec-1", "severity": "UNKNOWN", "category": "General",
                 "title": "T", "explanation": "E", "evidence": "Ev", "recommendedAction": "R", "affectedPath": null}
              ],
              "nextActions": []
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal("medium", result.Recommendations[0].Severity);
    }

    [Fact]
    public async Task AnalyzeAsync_NormalisesSeverityCaseInsensitive()
    {
        const string json = """
            {
              "scores": {"overall":60,"codeQuality":60,"security":60,"performance":60,"testing":60,"documentation":60},
              "findings": {"critical":0,"high":0,"medium":0,"low":0},
              "recommendations": [
                {"id":"r1","severity":"HIGH","category":"Security","title":"T","explanation":"E","evidence":"Ev","recommendedAction":"R","affectedPath":null},
                {"id":"r2","severity":"Critical","category":"Security","title":"T","explanation":"E","evidence":"Ev","recommendedAction":"R","affectedPath":null},
                {"id":"r3","severity":"low","category":"General","title":"T","explanation":"E","evidence":"Ev","recommendedAction":"R","affectedPath":null}
              ],
              "nextActions": []
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal("high",     result.Recommendations[0].Severity);
        Assert.Equal("critical", result.Recommendations[1].Severity);
        Assert.Equal("low",      result.Recommendations[2].Severity);
    }

    // ── 5. Nonexistent referenced file path → cleared ─────────────────────────

    [Fact]
    public async Task AnalyzeAsync_ClearsAffectedPath_WhenPathDoesNotExistInContext()
    {
        // Context has only one known file path
        var context = new GitHubRepoContext
        {
            FullName         = "acme/app",
            Branch           = "main",
            SampledFiles     = [new SampledFile { Path = "src/main.ts", Content = "// main" }],
            DependencyFiles  = ["package.json"],
            ConfigFiles      = [],
        };

        const string json = """
            {
              "scores": {"overall":70,"codeQuality":70,"security":70,"performance":70,"testing":70,"documentation":70},
              "findings": {"critical":0,"high":0,"medium":1,"low":0},
              "recommendations": [
                {"id":"rec-1","severity":"medium","category":"Security","title":"T",
                 "explanation":"E","evidence":"Ev","recommendedAction":"R",
                 "affectedPath":"src/nonexistent-invented-file.ts"}
              ],
              "nextActions": [{"priority":1,"action":"A","rationale":"R"}]
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", context);

        // The invented path should be cleared because it's not in the known paths
        Assert.Null(result.Recommendations[0].AffectedPath);
    }

    [Fact]
    public async Task AnalyzeAsync_KeepsAffectedPath_WhenPathExistsInContext()
    {
        var context = new GitHubRepoContext
        {
            FullName         = "acme/app",
            Branch           = "main",
            SampledFiles     = [new SampledFile { Path = "src/main.ts", Content = "// main" }],
            DependencyFiles  = ["package.json"],
            ConfigFiles      = [],
        };

        const string json = """
            {
              "scores": {"overall":70,"codeQuality":70,"security":70,"performance":70,"testing":70,"documentation":70},
              "findings": {"critical":0,"high":0,"medium":1,"low":0},
              "recommendations": [
                {"id":"rec-1","severity":"medium","category":"Security","title":"T",
                 "explanation":"E","evidence":"Ev","recommendedAction":"R",
                 "affectedPath":"package.json"}
              ],
              "nextActions": [{"priority":1,"action":"A","rationale":"R"}]
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", context);

        Assert.Equal("package.json", result.Recommendations[0].AffectedPath);
    }

    [Fact]
    public async Task AnalyzeAsync_AcceptsAnyPath_WhenNoContextAvailable()
    {
        // Without context, path validation is bypassed (no known paths to check against)
        const string json = """
            {
              "scores": {"overall":70,"codeQuality":70,"security":70,"performance":70,"testing":70,"documentation":70},
              "findings": {"critical":0,"high":0,"medium":1,"low":0},
              "recommendations": [
                {"id":"rec-1","severity":"medium","category":"Security","title":"T",
                 "explanation":"E","evidence":"Ev","recommendedAction":"R",
                 "affectedPath":"any/path/accepted.ts"}
              ],
              "nextActions": [{"priority":1,"action":"A","rationale":"R"}]
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal("any/path/accepted.ts", result.Recommendations[0].AffectedPath);
    }

    // ── 6. Fallback behavior ──────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_FallbackResponse_ContainsRepositoryUrl()
    {
        const string url = "https://github.com/acme/lost-repo";
        var result = await CreateSut(MockReturning("not json at all"))
            .AnalyzeAsync(url, "main", null);

        Assert.Equal(url,         result.Repository.Url);
        Assert.Equal("lost-repo", result.Repository.Name);
        Assert.Equal("Watsonx",   result.AnalysisSource);
    }

    [Fact]
    public async Task AnalyzeAsync_PropagatesWatsonxException()
    {
        var sut = CreateSut(MockThrowing(new WatsonxException("Simulated network error")));

        await Assert.ThrowsAsync<WatsonxException>(() =>
            sut.AnalyzeAsync("https://github.com/acme/app", "main", null));
    }

    // ── 7. Score validation (clamped to 0-100) ────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_ClampsOutOfRangeScores()
    {
        const string json = """
            {
              "scores": {"overall":150,"codeQuality":-10,"security":200,"performance":50,"testing":110,"documentation":-5},
              "findings": {"critical":0,"high":0,"medium":0,"low":0},
              "recommendations": [],
              "nextActions": []
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal(100, result.Scores.Overall);
        Assert.Equal(0,   result.Scores.CodeQuality);
        Assert.Equal(100, result.Scores.Security);
        Assert.Equal(100, result.Scores.Testing);
        Assert.Equal(0,   result.Scores.Documentation);
    }

    [Fact]
    public async Task AnalyzeAsync_FindingCounts_ClampedToNonNegative()
    {
        const string json = """
            {
              "scores": {"overall":70,"codeQuality":70,"security":70,"performance":70,"testing":70,"documentation":70},
              "findings": {"critical":-1,"high":-2,"medium":-3,"low":-4},
              "recommendations": [],
              "nextActions": []
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal(0, result.Findings.Critical);
        Assert.Equal(0, result.Findings.High);
        Assert.Equal(0, result.Findings.Medium);
        Assert.Equal(0, result.Findings.Low);
    }

    // ── 8. Empty findings list ────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_HandlesEmptyRecommendations_Gracefully()
    {
        const string json = """
            {
              "scores": {"overall":95,"codeQuality":95,"security":95,"performance":95,"testing":95,"documentation":95},
              "findings": {"critical":0,"high":0,"medium":0,"low":0},
              "recommendations": [],
              "nextActions": []
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Empty(result.Recommendations);
        Assert.Empty(result.NextActions);
        Assert.Equal(95, result.Scores.Overall);
    }

    // ── Model output with preamble (common LLM behaviour) ────────────────────

    [Fact]
    public async Task AnalyzeAsync_ExtractsJsonFromOutputWithPreamble()
    {
        var modelOutput = "Here is the analysis:\n" + ValidJson(70, 70, 70, 70, 70, 70);

        var result = await CreateSut(MockReturning(modelOutput))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal(70, result.Scores.Overall);
    }

    [Fact]
    public async Task AnalyzeAsync_ParsesJsonEmbeddedInMarkdownFence()
    {
        var modelOutput = "```json\n" + ValidJson(65, 65, 65, 65, 65, 65) + "\n```";

        var result = await CreateSut(MockReturning(modelOutput))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal(65, result.Scores.Overall);
    }

    // ── Auto-assigned IDs and priorities ─────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_UsesDefaultId_WhenRecommendationIdIsMissing()
    {
        const string json = """
            {
              "scores": {"overall":70,"codeQuality":70,"security":70,"performance":70,"testing":70,"documentation":70},
              "findings": {"critical":0,"high":0,"medium":0,"low":0},
              "recommendations": [
                {"id": "", "severity": "low", "category": "General", "title": "T",
                 "explanation": "E", "evidence": "Ev", "recommendedAction": "R", "affectedPath": null}
              ],
              "nextActions": [{"priority": 0, "action": "Act", "rationale": "Because"}]
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal("rec-1", result.Recommendations[0].Id);
    }

    [Fact]
    public async Task AnalyzeAsync_AssignsSequentialPriority_WhenPriorityIsZero()
    {
        const string json = """
            {
              "scores": {"overall":70,"codeQuality":70,"security":70,"performance":70,"testing":70,"documentation":70},
              "findings": {"critical":0,"high":0,"medium":0,"low":0},
              "recommendations": [],
              "nextActions": [
                {"priority": 0, "action": "First",  "rationale": "A"},
                {"priority": 0, "action": "Second", "rationale": "B"}
              ]
            }
            """;

        var result = await CreateSut(MockReturning(json))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal(1, result.NextActions[0].Priority);
        Assert.Equal(2, result.NextActions[1].Priority);
    }

    // ── Repository info ───────────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_ReturnsRepositoryInfo_WithCorrectNameAndBranch()
    {
        var result = await CreateSut(MockReturning(ValidJson()))
            .AnalyzeAsync("https://github.com/owner/my-project", "release/1.0", null);

        Assert.Equal("my-project",  result.Repository.Name);
        Assert.Equal("release/1.0", result.Repository.Branch);
        Assert.Equal("https://github.com/owner/my-project", result.Repository.Url);
    }

    // ── With repository context ───────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_WithContext_PopulatesRepoMetadata()
    {
        var context = new GitHubRepoContext
        {
            FullName          = "acme/app",
            Branch            = "main",
            Description       = "Test repository",
            PrimaryLanguage   = "C#",
            LanguageCounts    = new Dictionary<string, int> { ["C#"] = 10 },
            TotalFileCount    = 50,
            SampledFileCount  = 10,
            HasReadme         = true,
            HasTests          = true,
            HasCiConfig       = true,
            DependencyFiles   = ["MyApp.csproj"],
            ConfigFiles       = [".gitignore"],
            TopLevelDirectories = ["src", "tests"],
        };

        var result = await CreateSut(MockReturning(ValidJson()))
            .AnalyzeAsync("https://github.com/acme/app", "main", context);

        Assert.NotNull(result.RepoMetadata);
        Assert.Equal("Test repository", result.RepoMetadata!.Description);
        Assert.Equal("C#",              result.RepoMetadata.PrimaryLanguage);
        Assert.Equal(50,                result.RepoMetadata.TotalFileCount);
        Assert.True(result.RepoMetadata.HasReadme);
        Assert.True(result.RepoMetadata.HasTests);
        Assert.True(result.RepoMetadata.HasCiConfig);
    }

    [Fact]
    public async Task AnalyzeAsync_WithoutContext_RepoMetadataIsNull()
    {
        var result = await CreateSut(MockReturning(ValidJson()))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Null(result.RepoMetadata);
    }

    // ── AnalysisSource field ──────────────────────────────────────────────────

    [Fact]
    public async Task AnalyzeAsync_ReturnsWatsonxSource_OnSuccessfulParse()
    {
        var result = await CreateSut(MockReturning(ValidJson()))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal("Watsonx", result.AnalysisSource);
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsWatsonxSource_OnFallback()
    {
        // Even the fallback carries "Watsonx" because the service is the Watsonx implementation
        var result = await CreateSut(MockReturning("not json"))
            .AnalyzeAsync("https://github.com/acme/app", "main", null);

        Assert.Equal("Watsonx", result.AnalysisSource);
    }

    // ── WatsonxOptions configuration check ───────────────────────────────────

    [Theory]
    [InlineData("", "key", "project")]
    [InlineData("https://url", "", "project")]
    [InlineData("https://url", "key", "")]
    public void WatsonxOptions_IsNotConfigured_WhenAnyRequiredFieldMissing(
        string url, string apiKey, string projectId)
    {
        var opts = new WatsonxOptions { Url = url, ApiKey = apiKey, ProjectId = projectId };

        Assert.False(opts.IsConfigured);
    }

    [Fact]
    public void WatsonxOptions_IsConfigured_WhenAllRequiredFieldsPresent()
    {
        var opts = new WatsonxOptions
        {
            Url       = "https://us-south.ml.cloud.ibm.com",
            ApiKey    = "some-api-key",
            ProjectId = "some-project-id",
        };

        Assert.True(opts.IsConfigured);
    }
}
