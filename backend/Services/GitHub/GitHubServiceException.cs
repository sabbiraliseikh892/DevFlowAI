namespace DevFlowAI.API.Services.GitHub;

/// <summary>
/// Exception thrown when the GitHub API returns an error or the repository
/// cannot be accessed (private, not found, rate-limited, etc.).
/// </summary>
public sealed class GitHubServiceException : Exception
{
    public int? HttpStatusCode { get; }

    public GitHubServiceException(string message) : base(message) { }

    public GitHubServiceException(string message, int httpStatusCode)
        : base(message)
    {
        HttpStatusCode = httpStatusCode;
    }

    public GitHubServiceException(string message, Exception innerException)
        : base(message, innerException) { }
}
