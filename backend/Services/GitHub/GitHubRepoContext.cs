namespace DevFlowAI.API.Services.GitHub;

/// <summary>
/// Collected metadata and sampled file content from a public GitHub repository.
/// Passed to the analysis service so the AI model receives concrete evidence
/// rather than having to infer structure from the URL alone.
/// </summary>
public sealed class GitHubRepoContext
{
    /// <summary>Owner/repo form, e.g. "facebook/react".</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Branch or tag that was inspected.</summary>
    public string Branch { get; init; } = string.Empty;

    /// <summary>Repository description from the GitHub API, if available.</summary>
    public string? Description { get; init; }

    /// <summary>Primary language as reported by GitHub.</summary>
    public string? PrimaryLanguage { get; init; }

    /// <summary>Detected programming languages (extension → count).</summary>
    public IReadOnlyDictionary<string, int> LanguageCounts { get; init; }
        = new Dictionary<string, int>();

    /// <summary>Total number of source files found (before filtering).</summary>
    public int TotalFileCount { get; init; }

    /// <summary>Number of files included in the sampled context.</summary>
    public int SampledFileCount { get; init; }

    /// <summary>Whether a README file was found at the repo root.</summary>
    public bool HasReadme { get; init; }

    /// <summary>Whether any test files or test directories were detected.</summary>
    public bool HasTests { get; init; }

    /// <summary>Whether a CI configuration file was found (.github/workflows, .travis.yml, etc.).</summary>
    public bool HasCiConfig { get; init; }

    /// <summary>Dependency manifest filenames detected (package.json, *.csproj, requirements.txt, etc.).</summary>
    public IReadOnlyList<string> DependencyFiles { get; init; } = [];

    /// <summary>Configuration filenames detected at the repo root.</summary>
    public IReadOnlyList<string> ConfigFiles { get; init; } = [];

    /// <summary>
    /// Sampled file entries: path → content (truncated to a safe size).
    /// Only non-binary, non-secret source files are included.
    /// </summary>
    public IReadOnlyList<SampledFile> SampledFiles { get; init; } = [];

    /// <summary>Top-level directories detected in the repo tree.</summary>
    public IReadOnlyList<string> TopLevelDirectories { get; init; } = [];
}

/// <summary>A single file sampled from the repository.</summary>
public sealed class SampledFile
{
    public string Path { get; init; } = string.Empty;
    /// <summary>First <see cref="MaxContentLength"/> characters of the file content.</summary>
    public string Content { get; init; } = string.Empty;
    public const int MaxContentLength = 3000;
}
