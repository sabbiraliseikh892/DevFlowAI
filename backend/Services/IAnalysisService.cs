using DevFlowAI.API.DTOs;
using DevFlowAI.API.Services.GitHub;

namespace DevFlowAI.API.Services;

/// <summary>
/// Abstraction for repository analysis.
/// Swap MockAnalysisService for a real LLM-backed implementation
/// (IBM watsonx, OpenAI, etc.) without touching the controller or frontend contract.
///
/// The optional <paramref name="context"/> parameter carries real repository
/// metadata fetched from GitHub. When null (e.g. URL validation failed or
/// GitHub is unreachable), implementations fall back to URL-only analysis.
/// </summary>
public interface IAnalysisService
{
    Task<AnalysisResponseDto> AnalyzeAsync(
        string repositoryUrl,
        string branch,
        GitHubRepoContext? context = null,
        CancellationToken ct = default);
}
