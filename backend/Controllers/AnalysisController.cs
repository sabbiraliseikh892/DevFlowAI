using DevFlowAI.API.DTOs;
using DevFlowAI.API.Services;
using DevFlowAI.API.Services.GitHub;
using Microsoft.AspNetCore.Mvc;

namespace DevFlowAI.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly IAnalysisService _analysisService;
    private readonly IGitHubService   _gitHubService;

    public AnalysisController(
        IAnalysisService analysisService,
        IGitHubService   gitHubService)
    {
        _analysisService = analysisService;
        _gitHubService   = gitHubService;
    }

    /// <summary>
    /// POST /api/analysis/analyze
    /// Submit a public GitHub repository URL for AI-powered developer workflow analysis.
    ///
    /// Workflow:
    ///   1. Validate that the URL is a public GitHub HTTPS URL.
    ///   2. Fetch real repository metadata and sampled file content from GitHub.
    ///   3. Pass the context to the analysis service (Mock or Watsonx).
    ///
    /// Returns scores, findings, recommendations, suggested next actions, and
    /// (when GitHub inspection succeeded) real repository metadata.
    /// </summary>
    [HttpPost("analyze")]
    [ProducesResponseType(typeof(AnalysisResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Analyze(
        [FromBody] AnalysisRequestDto request,
        CancellationToken ct)
    {
        // ── 1. Basic null/empty check ─────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(request.RepositoryUrl))
        {
            return BadRequest(new ProblemDetails
            {
                Title  = "Invalid request",
                Detail = "repositoryUrl is required.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // ── 2. GitHub URL validation ──────────────────────────────────────────
        if (!GitHubUrlValidator.TryParse(request.RepositoryUrl, out var owner, out var repo))
        {
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Unsupported repository URL",
                Detail = "Only public GitHub repositories are supported. " +
                         "Please provide a URL in the form: https://github.com/owner/repository",
                Status = StatusCodes.Status422UnprocessableEntity
            });
        }

        var effectiveBranch = string.IsNullOrWhiteSpace(request.Branch)
            ? "main"
            : request.Branch;

        // ── 3. Fetch real repository context from GitHub ──────────────────────
        GitHubRepoContext? context = null;
        try
        {
            context = await _gitHubService.GetRepoContextAsync(
                owner, repo, effectiveBranch, ct);
        }
        catch (GitHubServiceException ex) when (ex.HttpStatusCode is 404)
        {
            // Repository or branch not found.
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Repository not found",
                Detail = ex.Message,
                Status = StatusCodes.Status422UnprocessableEntity
            });
        }
        catch (GitHubServiceException ex) when (ex.HttpStatusCode is 403 or 401)
        {
            // Repository exists but is private.
            return UnprocessableEntity(new ProblemDetails
            {
                Title  = "Repository not accessible",
                Detail = ex.Message,
                Status = StatusCodes.Status422UnprocessableEntity
            });
        }
        catch (GitHubServiceException ex) when (ex.HttpStatusCode is 429)
        {
            return StatusCode(429, new ProblemDetails
            {
                Title  = "GitHub API rate limit exceeded",
                Detail = ex.Message,
                Status = 429
            });
        }
        catch (GitHubServiceException)
        {
            // Network error or other GitHub issue — proceed without live context.
            // The analysis service will fall back to URL-only analysis.
            context = null;
        }

        // Use the branch returned from GitHub (respects default branch) when available.
        var canonicalBranch = context?.Branch ?? effectiveBranch;

        // ── 4. Run analysis ───────────────────────────────────────────────────
        var result = await _analysisService.AnalyzeAsync(
            GitHubUrlValidator.Normalise(owner, repo),
            canonicalBranch,
            context,
            ct);

        return Ok(result);
    }
}
