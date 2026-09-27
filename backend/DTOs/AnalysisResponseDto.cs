namespace DevFlowAI.API.DTOs;

public record AnalysisResponseDto(
    RepositoryInfoDto Repository,
    ScoresDto Scores,
    FindingsSummaryDto Findings,
    IReadOnlyList<RecommendationDto> Recommendations,
    IReadOnlyList<NextActionDto> NextActions,
    DateTime AnalyzedAt,
    /// <summary>"Watsonx" when powered by IBM watsonx.ai; "Mock" otherwise.</summary>
    string AnalysisSource = "Mock",
    RepoMetadataDto? RepoMetadata = null  // null in Mock mode or when GitHub fetch failed
);

public record RepositoryInfoDto(
    string Url,
    string Name,
    string Branch
);

/// <summary>
/// Six-dimension health scores (0–100).
/// Overall is reported separately; the five dimensions are averaged to produce it.
/// </summary>
public record ScoresDto(
    int Overall,
    int CodeQuality,
    int Security,
    int Performance,
    int Testing,
    int Documentation
);

public record FindingsSummaryDto(
    int Critical,
    int High,
    int Medium,
    int Low
);

/// <summary>
/// A single actionable finding.
/// Severity is one of: critical | high | medium | low
/// </summary>
public record RecommendationDto(
    string Id,
    string Severity,
    string Category,
    string Title,
    /// <summary>Human-readable explanation of why this matters.</summary>
    string Explanation,
    /// <summary>Concrete evidence from the repository, or "Evidence unavailable" when none exists.</summary>
    string Evidence,
    /// <summary>Specific action the developer should take.</summary>
    string RecommendedAction,
    /// <summary>Affected file or path, null when not applicable.</summary>
    string? AffectedPath,
    int? Line = null
);

public record NextActionDto(
    int Priority,
    string Action,
    string Rationale
);

/// <summary>
/// Real repository metadata fetched from GitHub.
/// Only present in the response when GitHub inspection succeeded.
/// </summary>
public record RepoMetadataDto(
    string? Description,
    string? PrimaryLanguage,
    IReadOnlyDictionary<string, int> LanguageCounts,
    int TotalFileCount,
    int SampledFileCount,
    bool HasReadme,
    bool HasTests,
    bool HasCiConfig,
    IReadOnlyList<string> DependencyFiles,
    IReadOnlyList<string> ConfigFiles,
    IReadOnlyList<string> TopLevelDirectories
);
