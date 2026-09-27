using DevFlowAI.API.Controllers;
using DevFlowAI.API.DTOs;
using DevFlowAI.API.Services;
using DevFlowAI.API.Services.GitHub;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace DevFlowAI.Tests;

/// <summary>
/// Unit tests for <see cref="AnalysisController"/>.
/// The controller is instantiated directly — no web host required.
///
/// Phase 6 additions cover:
///   - GitHub URL validation (422 for non-GitHub URLs)
///   - 404 / 403 from GitHub → 422 responses
///   - GitHub network failure → proceeds with null context
///   - Rate-limit → 429
///   - Context forwarded to IAnalysisService
/// </summary>
public class AnalysisControllerTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AnalysisResponseDto BuildSampleResponse(
        string url    = "https://github.com/acme/app",
        string name   = "app",
        string branch = "main") =>
        new(
            Repository    : new RepositoryInfoDto(url, name, branch),
            Scores        : new ScoresDto(80, 82, 75, 90, 78, 85),
            Findings      : new FindingsSummaryDto(0, 1, 2, 3),
            Recommendations: [
                new RecommendationDto("rec-1", "high", "Security",
                    "Outdated dep",
                    "lodash < 4.17.21 has a prototype pollution vulnerability.",
                    "package.json specifies lodash@4.17.15.",
                    "Upgrade lodash to 4.17.21.",
                    "package.json", null)
            ],
            NextActions   : [
                new NextActionDto(1, "Upgrade deps", "Known CVE.")
            ],
            AnalyzedAt    : DateTime.UtcNow);

    private static GitHubRepoContext BuildSampleContext(
        string owner = "acme", string repo = "app") =>
        new GitHubRepoContext
        {
            FullName    = $"{owner}/{repo}",
            Branch      = "main",
            HasReadme   = true,
            HasTests    = true,
            HasCiConfig = true,
        };

    private static AnalysisController CreateController(
        IAnalysisService svc,
        IGitHubService?  ghSvc = null)
    {
        var github = ghSvc ?? Mock.Of<IGitHubService>();

        var controller = new AnalysisController(svc, github);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
        return controller;
    }

    // ── Success path ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_Returns200_WithAnalysisResult_WhenUrlIsValid()
    {
        var expected = BuildSampleResponse();
        var mockSvc  = new Mock<IAnalysisService>();
        var mockGh   = new Mock<IGitHubService>();
        var context  = BuildSampleContext();

        mockGh.Setup(g => g.GetRepoContextAsync("acme", "app", "main", It.IsAny<CancellationToken>()))
              .ReturnsAsync(context);
        mockSvc.Setup(s => s.AnalyzeAsync(
                "https://github.com/acme/app", "main",
                context, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(mockSvc.Object, mockGh.Object);

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto("https://github.com/acme/app", "main"),
            CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);
        var body = Assert.IsType<AnalysisResponseDto>(okResult.Value);
        Assert.Equal("app",  body.Repository.Name);
        Assert.Equal(80,     body.Scores.Overall);
    }

    [Fact]
    public async Task Analyze_PassesBranch_ToAnalysisService()
    {
        var expected = BuildSampleResponse(branch: "develop");
        var mockSvc  = new Mock<IAnalysisService>();
        var mockGh   = new Mock<IGitHubService>();

        // Context must report Branch = "develop" so the controller uses it
        var ctx = new GitHubRepoContext { FullName = "acme/app", Branch = "develop", HasReadme = true };

        mockGh.Setup(g => g.GetRepoContextAsync(
                It.IsAny<string>(), It.IsAny<string>(), "develop",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ctx);
        mockSvc.Setup(s => s.AnalyzeAsync(
                It.IsAny<string>(), "develop",
                It.IsAny<GitHubRepoContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(mockSvc.Object, mockGh.Object);

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto("https://github.com/acme/app", "develop"),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(actionResult);
        mockSvc.Verify(
            s => s.AnalyzeAsync(
                It.IsAny<string>(), "develop",
                It.IsAny<GitHubRepoContext?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Analyze_DefaultsBranchToMain_WhenBranchIsWhitespace()
    {
        var expected = BuildSampleResponse();
        var mockSvc  = new Mock<IAnalysisService>();
        var mockGh   = new Mock<IGitHubService>();

        mockGh.Setup(g => g.GetRepoContextAsync(
                It.IsAny<string>(), It.IsAny<string>(), "main",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleContext());
        mockSvc.Setup(s => s.AnalyzeAsync(
                It.IsAny<string>(), "main",
                It.IsAny<GitHubRepoContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(mockSvc.Object, mockGh.Object);

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto("https://github.com/acme/app", "   "),
            CancellationToken.None);

        Assert.IsType<OkObjectResult>(actionResult);
        mockSvc.Verify(
            s => s.AnalyzeAsync(
                It.IsAny<string>(), "main",
                It.IsAny<GitHubRepoContext?>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Validation: missing / empty repositoryUrl ─────────────────────────────

    [Fact]
    public async Task Analyze_Returns400_WhenRepositoryUrlIsEmpty()
    {
        var controller = CreateController(Mock.Of<IAnalysisService>());

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto("", "main"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
    }

    [Fact]
    public async Task Analyze_Returns400_WhenRepositoryUrlIsWhitespace()
    {
        var controller = CreateController(Mock.Of<IAnalysisService>());

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto("   ", "main"),
            CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(actionResult);
    }

    [Fact]
    public async Task Analyze_Returns400_ContainsProblemDetails_WithDetailMessage()
    {
        var controller = CreateController(Mock.Of<IAnalysisService>());

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto("", "main"),
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult);
        var problem    = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal("repositoryUrl is required.", problem.Detail);
        Assert.Equal("Invalid request",            problem.Title);
    }

    [Fact]
    public async Task Analyze_DoesNotCallService_WhenRepositoryUrlIsEmpty()
    {
        var mockSvc    = new Mock<IAnalysisService>();
        var controller = CreateController(mockSvc.Object);

        await controller.Analyze(
            new AnalysisRequestDto("", "main"),
            CancellationToken.None);

        mockSvc.Verify(
            s => s.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GitHubRepoContext?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── GitHub URL validation ─────────────────────────────────────────────────

    [Theory]
    [InlineData("https://gitlab.com/owner/repo")]
    [InlineData("https://bitbucket.org/owner/repo")]
    [InlineData("http://github.com/owner/repo")]   // HTTP not HTTPS
    [InlineData("owner/repo")]                      // short form not supported
    [InlineData("not-a-url")]
    public async Task Analyze_Returns422_ForNonGitHubUrls(string url)
    {
        var controller = CreateController(Mock.Of<IAnalysisService>());

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto(url, "main"),
            CancellationToken.None);

        var result = Assert.IsType<UnprocessableEntityObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal("Unsupported repository URL", problem.Title);
    }

    [Theory]
    [InlineData("https://github.com/owner/repo")]
    [InlineData("https://github.com/owner/repo/")]
    [InlineData("https://github.com/owner/repo.git")]
    [InlineData("https://github.com/owner/repo/tree/main")]
    public async Task Analyze_Returns200_ForValidGitHubUrls(string url)
    {
        var expected = BuildSampleResponse(url: "https://github.com/owner/repo", name: "repo");
        var mockSvc  = new Mock<IAnalysisService>();
        var mockGh   = new Mock<IGitHubService>();
        mockGh.Setup(g => g.GetRepoContextAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildSampleContext("owner", "repo"));
        mockSvc.Setup(s => s.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<GitHubRepoContext?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(mockSvc.Object, mockGh.Object);
        var result = await controller.Analyze(
            new AnalysisRequestDto(url, "main"), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    // ── GitHub service errors ─────────────────────────────────────────────────

    [Fact]
    public async Task Analyze_Returns422_WhenGitHubReturns404()
    {
        var mockSvc = new Mock<IAnalysisService>();
        var mockGh  = new Mock<IGitHubService>();
        mockGh.Setup(g => g.GetRepoContextAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GitHubServiceException("not found", 404));

        var controller = CreateController(mockSvc.Object, mockGh.Object);

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto("https://github.com/owner/repo", "main"),
            CancellationToken.None);

        var result = Assert.IsType<UnprocessableEntityObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
    }

    [Fact]
    public async Task Analyze_Returns422_WhenGitHubReturns403()
    {
        var mockSvc = new Mock<IAnalysisService>();
        var mockGh  = new Mock<IGitHubService>();
        mockGh.Setup(g => g.GetRepoContextAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GitHubServiceException("forbidden", 403));

        var controller = CreateController(mockSvc.Object, mockGh.Object);

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto("https://github.com/owner/repo", "main"),
            CancellationToken.None);

        var result = Assert.IsType<UnprocessableEntityObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, result.StatusCode);
    }

    [Fact]
    public async Task Analyze_Returns429_WhenGitHubRateLimited()
    {
        var mockSvc = new Mock<IAnalysisService>();
        var mockGh  = new Mock<IGitHubService>();
        mockGh.Setup(g => g.GetRepoContextAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GitHubServiceException("rate limited", 429));

        var expected = BuildSampleResponse();
        var controller = CreateController(mockSvc.Object, mockGh.Object);

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto("https://github.com/owner/repo", "main"),
            CancellationToken.None);

        // 429 should NOT fall through to analysis — it's a hard error.
        Assert.IsType<ObjectResult>(actionResult);
        var result = (ObjectResult)actionResult;
        Assert.Equal(429, result.StatusCode);
    }

    [Fact]
    public async Task Analyze_ProceedsWithNullContext_WhenGitHubNetworkFails()
    {
        // A generic GitHubServiceException (no status code) means network failure.
        // The controller should fall back to URL-only analysis rather than returning 500.
        var mockSvc = new Mock<IAnalysisService>();
        var mockGh  = new Mock<IGitHubService>();
        var expected = BuildSampleResponse();

        mockGh.Setup(g => g.GetRepoContextAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GitHubServiceException("network error"));
        mockSvc.Setup(s => s.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(mockSvc.Object, mockGh.Object);

        var actionResult = await controller.Analyze(
            new AnalysisRequestDto("https://github.com/owner/repo", "main"),
            CancellationToken.None);

        // Should still return 200 (graceful degradation).
        var ok = Assert.IsType<OkObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);

        // Service must have been called with null context.
        mockSvc.Verify(
            s => s.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                null, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Context is forwarded to the analysis service ──────────────────────────

    [Fact]
    public async Task Analyze_ForwardsGitHubContext_ToAnalysisService()
    {
        var ctx      = BuildSampleContext();
        var expected = BuildSampleResponse();
        var mockSvc  = new Mock<IAnalysisService>();
        var mockGh   = new Mock<IGitHubService>();

        mockGh.Setup(g => g.GetRepoContextAsync("acme", "app", "main", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ctx);
        mockSvc.Setup(s => s.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                ctx, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController(mockSvc.Object, mockGh.Object);

        await controller.Analyze(
            new AnalysisRequestDto("https://github.com/acme/app", "main"),
            CancellationToken.None);

        mockSvc.Verify(
            s => s.AnalyzeAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                ctx, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
