namespace DevFlowAI.API.Services.GitHub;

/// <summary>
/// Validates and parses public GitHub repository URLs.
/// Only public github.com repositories are supported for the MVP.
/// </summary>
public static class GitHubUrlValidator
{
    private static readonly string[] AllowedHosts =
        ["github.com", "www.github.com"];

    /// <summary>
    /// Returns true and populates <paramref name="owner"/> / <paramref name="repo"/>
    /// when <paramref name="url"/> is a valid public GitHub HTTPS URL.
    ///
    /// Accepted forms:
    ///   https://github.com/owner/repo
    ///   https://github.com/owner/repo.git
    ///   https://github.com/owner/repo/tree/branch
    ///   https://www.github.com/owner/repo
    /// </summary>
    public static bool TryParse(
        string? url,
        out string owner,
        out string repo)
    {
        owner = string.Empty;
        repo  = string.Empty;

        if (string.IsNullOrWhiteSpace(url))
            return false;

        // Must be an absolute HTTPS URL.
        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
            return false;

        if (!uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!AllowedHosts.Contains(
                uri.Host, StringComparer.OrdinalIgnoreCase))
            return false;

        // Path segments: ["", "owner", "repo", ...]
        var segments = uri.AbsolutePath
            .TrimEnd('/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length < 2)
            return false;

        owner = segments[0];
        repo  = segments[1].Replace(".git", "", StringComparison.OrdinalIgnoreCase);

        return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repo);
    }

    /// <summary>
    /// Returns a normalised canonical URL: https://github.com/{owner}/{repo}
    /// </summary>
    public static string Normalise(string owner, string repo) =>
        $"https://github.com/{owner}/{repo}";
}
