namespace DevFlowAI.API.Services.GitHub;

/// <summary>
/// Contract for fetching public GitHub repository context.
/// </summary>
public interface IGitHubService
{
    /// <summary>
    /// Retrieves metadata and sampled source files for a public GitHub repository.
    /// </summary>
    /// <param name="owner">Repository owner (user or organisation).</param>
    /// <param name="repo">Repository name.</param>
    /// <param name="branch">Branch name. If null or empty, the repository's default branch is used.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="GitHubRepoContext"/> containing metadata and sampled file content.</returns>
    /// <exception cref="GitHubServiceException">
    /// Thrown when the repository is not found, private, rate-limited, or unreachable.
    /// </exception>
    Task<GitHubRepoContext> GetRepoContextAsync(
        string owner,
        string repo,
        string? branch,
        CancellationToken ct = default);
}
