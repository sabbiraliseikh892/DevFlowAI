using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace DevFlowAI.API.Services.GitHub;

/// <summary>
/// Fetches public GitHub repository metadata and sampled file content using
/// the GitHub REST API v3 — no OAuth required for public repositories.
///
/// Rate limits (unauthenticated): 60 requests / hour per IP.
/// Set the GITHUB_TOKEN environment variable to raise the limit to 5,000/hour.
/// The token is used ONLY for API authentication; it is never sent to the frontend
/// or included in any AI prompt.
///
/// Files are fetched using the Git Trees API (recursive) to avoid per-directory
/// pagination, then individual blobs are fetched for a filtered, budget-limited
/// subset using the raw.githubusercontent.com endpoint.
/// </summary>
public sealed class GitHubService : IGitHubService
{
    private const string ApiBase    = "https://api.github.com";
    private const string UserAgent  = "DevFlowAI/1.0";

    private readonly HttpClient _http;
    private readonly ILogger<GitHubService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    };

    public GitHubService(HttpClient http, ILogger<GitHubService> logger)
    {
        _http   = http;
        _logger = logger;

        // Set common headers once on the shared HttpClient.
        if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
            _http.DefaultRequestHeaders.Add("User-Agent", UserAgent);

        // Optional: use GITHUB_TOKEN for higher rate limits.
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (!string.IsNullOrWhiteSpace(token) &&
            !_http.DefaultRequestHeaders.Contains("Authorization"))
        {
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task<GitHubRepoContext> GetRepoContextAsync(
        string owner,
        string repo,
        string? branch,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "GitHubService: fetching context for {Owner}/{Repo} @ {Branch}",
            owner, repo, branch ?? "(default)");

        // 1. Fetch repository info (description, primary language, default branch).
        var repoInfo = await GetRepoInfoAsync(owner, repo, ct);
        var effectiveBranch = !string.IsNullOrWhiteSpace(branch)
            ? branch
            : (repoInfo?.DefaultBranch ?? "main");

        // 2. Fetch the full file tree recursively using the Git Trees API.
        var allPaths = await GetFileTreeAsync(owner, repo, effectiveBranch, ct);

        if (allPaths.Count == 0)
        {
            _logger.LogInformation(
                "GitHubService: no files found in {Owner}/{Repo} @ {Branch}",
                owner, repo, effectiveBranch);
        }

        // 3. Analyse the tree to build metadata (languages, CI, tests, etc.).
        var languageCounts     = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dependencyFiles    = new List<string>();
        var configFiles        = new List<string>();
        var topLevelDirs       = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool hasReadme         = false;
        bool hasTests          = false;
        bool hasCi             = false;

        foreach (var path in allPaths)
        {
            var fileName = Path.GetFileName(path);
            var segments = path.Split('/');

            // Track top-level directories
            if (segments.Length > 1)
                topLevelDirs.Add(segments[0]);

            // README check
            if (segments.Length == 1 &&
                fileName.StartsWith("README", StringComparison.OrdinalIgnoreCase))
                hasReadme = true;

            // CI check
            if (RepoFileFilter.IsCiFile(path)) hasCi = true;

            // Test detection
            if (RepoFileFilter.IsTestFile(path)) hasTests = true;

            // Dependency files
            if (RepoFileFilter.IsDependencyFile(path) && !dependencyFiles.Contains(fileName))
                dependencyFiles.Add(fileName);

            // Config files at the root level (one directory deep at most)
            if (segments.Length <= 2)
            {
                var ext = Path.GetExtension(fileName).ToLowerInvariant();
                if (ext is ".json" or ".yaml" or ".yml" or ".toml" or ".xml" or
                    ".editorconfig" or ".eslintrc" or ".prettierrc" or
                    ".gitignore" or ".gitattributes" or ".env.example")
                {
                    if (!configFiles.Contains(fileName))
                        configFiles.Add(fileName);
                }
            }

            // Language counting (source files only)
            if (!RepoFileFilter.IsExcluded(path))
            {
                var ext  = Path.GetExtension(path);
                var lang = RepoFileFilter.ExtensionToLanguage(ext);
                if (lang is not null)
                    languageCounts[lang] = languageCounts.GetValueOrDefault(lang) + 1;
            }
        }

        // 4. Select files to sample (prioritise important ones, respect budget).
        var filesToFetch = SelectFilesForSampling(allPaths);

        // 5. Fetch sampled file contents.
        var sampledFiles = await FetchFileContentsAsync(
            owner, repo, effectiveBranch, filesToFetch, ct);

        var ctx = new GitHubRepoContext
        {
            FullName          = $"{owner}/{repo}",
            Branch            = effectiveBranch,
            Description       = repoInfo?.Description,
            PrimaryLanguage   = repoInfo?.Language,
            LanguageCounts    = languageCounts,
            TotalFileCount    = allPaths.Count,
            SampledFileCount  = sampledFiles.Count,
            HasReadme         = hasReadme,
            HasTests          = hasTests,
            HasCiConfig       = hasCi,
            DependencyFiles   = dependencyFiles,
            ConfigFiles       = configFiles,
            SampledFiles      = sampledFiles,
            TopLevelDirectories = [.. topLevelDirs.OrderBy(d => d)],
        };

        _logger.LogInformation(
            "GitHubService: context built for {FullName}. " +
            "Files: {Total}, Sampled: {Sampled}, Languages: {Langs}",
            ctx.FullName, ctx.TotalFileCount, ctx.SampledFileCount,
            string.Join(", ", languageCounts.Keys));

        return ctx;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    private async Task<GitHubRepoInfo?> GetRepoInfoAsync(
        string owner, string repo, CancellationToken ct)
    {
        var url = $"{ApiBase}/repos/{owner}/{repo}";
        try
        {
            using var response = await _http.GetAsync(url, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new GitHubServiceException(
                    $"Repository '{owner}/{repo}' was not found on GitHub. " +
                    "Make sure the URL is correct and the repository is public.",
                    404);

            if (response.StatusCode == HttpStatusCode.Forbidden ||
                response.StatusCode == HttpStatusCode.Unauthorized)
                throw new GitHubServiceException(
                    "Access to this repository is forbidden. " +
                    "Only public GitHub repositories are supported.",
                    (int)response.StatusCode);

            if ((int)response.StatusCode == 429 ||
                response.Headers.Contains("x-ratelimit-remaining") &&
                response.Headers.GetValues("x-ratelimit-remaining")
                    .FirstOrDefault() == "0")
            {
                throw new GitHubServiceException(
                    "GitHub API rate limit exceeded. " +
                    "Set the GITHUB_TOKEN environment variable for a higher limit.",
                    429);
            }

            if (!response.IsSuccessStatusCode)
                throw new GitHubServiceException(
                    $"GitHub API returned HTTP {(int)response.StatusCode} for {owner}/{repo}.",
                    (int)response.StatusCode);

            var body = await response.Content.ReadAsStringAsync(ct);
            return JsonSerializer.Deserialize<GitHubRepoInfo>(body, JsonOpts);
        }
        catch (GitHubServiceException) { throw; }
        catch (Exception ex)
        {
            throw new GitHubServiceException(
                "Failed to reach the GitHub API. Check your internet connection.", ex);
        }
    }

    private async Task<List<string>> GetFileTreeAsync(
        string owner, string repo, string branch, CancellationToken ct)
    {
        var url = $"{ApiBase}/repos/{owner}/{repo}/git/trees/{branch}?recursive=1";
        try
        {
            using var response = await _http.GetAsync(url, ct);

            // A 404 here usually means the branch doesn't exist.
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new GitHubServiceException(
                    $"Branch '{branch}' not found in '{owner}/{repo}'. " +
                    "Check the branch name and try again.",
                    404);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "GitHub tree API returned {Status} for {Owner}/{Repo}@{Branch}",
                    (int)response.StatusCode, owner, repo, branch);
                return [];
            }

            var body = await response.Content.ReadAsStringAsync(ct);
            var tree = JsonSerializer.Deserialize<GitHubTreeResponse>(body, JsonOpts);

            if (tree?.Tree is null) return [];

            // Filter: keep only blob (file) entries, not tree (directory) entries.
            return tree.Tree
                .Where(e => e.Type == "blob" && !string.IsNullOrWhiteSpace(e.Path))
                .Select(e => e.Path!)
                .ToList();
        }
        catch (GitHubServiceException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch file tree for {Owner}/{Repo}", owner, repo);
            return [];
        }
    }

    private static List<string> SelectFilesForSampling(IReadOnlyList<string> allPaths)
    {
        var selected  = new List<string>();
        var remaining = new List<string>();

        // Pass 1: always include priority files
        foreach (var path in allPaths)
        {
            if (RepoFileFilter.IsExcluded(path)) continue;

            var fileName = Path.GetFileName(path);
            if (IsPriorityFile(path) || IsPriorityFile(fileName))
                selected.Add(path);
            else
                remaining.Add(path);
        }

        // Pass 2: fill up to the max with remaining files
        foreach (var path in remaining)
        {
            if (selected.Count >= RepoFileFilter.MaxSampledFiles) break;
            selected.Add(path);
        }

        return selected;
    }

    private static bool IsPriorityFile(string pathOrName)
    {
        var n = Path.GetFileName(pathOrName).ToLowerInvariant();
        return n is "readme.md" or "readme" or "readme.txt" or "readme.rst"
            or "package.json" or "requirements.txt" or "pipfile" or "go.mod"
            or "cargo.toml" or "pom.xml" or ".gitignore"
            or "dockerfile" or "docker-compose.yml" or "docker-compose.yaml"
            or "makefile"
            || Path.GetExtension(n) is ".csproj" or ".fsproj";
    }

    private async Task<List<SampledFile>> FetchFileContentsAsync(
        string owner,
        string repo,
        string branch,
        IReadOnlyList<string> paths,
        CancellationToken ct)
    {
        var results   = new List<SampledFile>();
        var totalChars = 0;

        foreach (var path in paths)
        {
            if (totalChars >= RepoFileFilter.MaxTotalContextChars) break;
            if (ct.IsCancellationRequested) break;

            try
            {
                // Use raw.githubusercontent.com — counts towards rate limit
                // but is cheaper than the blob API for text content.
                var rawUrl = $"https://raw.githubusercontent.com/{owner}/{repo}/{branch}/{path}";
                var content = await _http.GetStringAsync(rawUrl, ct);

                // Truncate at the per-file limit
                if (content.Length > SampledFile.MaxContentLength)
                    content = content[..SampledFile.MaxContentLength] + "\n[… truncated …]";

                results.Add(new SampledFile { Path = path, Content = content });
                totalChars += content.Length;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex, "Could not fetch content for {Path} in {Owner}/{Repo}", path, owner, repo);
                // Skip this file but continue with others.
            }
        }

        return results;
    }

    // ── Internal GitHub API response DTOs ─────────────────────────────────────

    private sealed class GitHubRepoInfo
    {
        public string?  Description    { get; set; }
        public string?  Language       { get; set; }
        [JsonPropertyName("default_branch")]
        public string?  DefaultBranch  { get; set; }
    }

    private sealed class GitHubTreeResponse
    {
        public List<TreeEntry>? Tree { get; set; }
        public bool Truncated { get; set; }
    }

    private sealed class TreeEntry
    {
        public string? Path { get; set; }
        public string? Type { get; set; } // "blob" | "tree"
        public int?    Size { get; set; }
    }
}
