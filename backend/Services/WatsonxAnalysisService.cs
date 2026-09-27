using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DevFlowAI.API.DTOs;
using DevFlowAI.API.Services.GitHub;
using DevFlowAI.API.Services.Watsonx;
using Microsoft.Extensions.Logging;

namespace DevFlowAI.API.Services;

/// <summary>
/// Production implementation of <see cref="IAnalysisService"/> backed by IBM watsonx.ai.
///
/// Workflow:
///   1. Build a structured prompt describing the repository.
///      When a <see cref="GitHubRepoContext"/> is supplied, real repository metadata
///      and sampled file content are embedded so the model reasons from evidence.
///   2. Send prompt → <see cref="WatsonxHttpClient"/> (handles IAM auth, HTTP).
///   3. Extract and parse the JSON block from the model output.
///   4. Validate referenced file paths against the known repository context.
///   5. Map parsed result → DTO types so the controller and frontend require zero changes.
///
/// If the model returns unparseable JSON or a network failure occurs, the exception
/// propagates to the controller which returns 500.  The provider selection in
/// Program.cs will fall back to <see cref="MockAnalysisService"/> when credentials
/// are absent, so this service is never called without valid configuration.
/// </summary>
public sealed class WatsonxAnalysisService : IAnalysisService
{
    private readonly IWatsonxHttpClient _client;
    private readonly ILogger<WatsonxAnalysisService> _logger;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
    };

    public WatsonxAnalysisService(
        IWatsonxHttpClient client,
        ILogger<WatsonxAnalysisService> logger)
    {
        _client = client;
        _logger  = logger;
    }

    // ── IAnalysisService ──────────────────────────────────────────────────────

    public async Task<AnalysisResponseDto> AnalyzeAsync(
        string repositoryUrl,
        string branch,
        GitHubRepoContext? context = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "WatsonxAnalysisService: analysing {Repo} @ {Branch} " +
            "(context available: {HasContext})",
            repositoryUrl, branch, context is not null);

        var prompt = BuildPrompt(repositoryUrl, branch, context);
        string rawOutput;

        try
        {
            rawOutput = await _client.GenerateTextAsync(prompt, ct);
        }
        catch (WatsonxException ex)
        {
            _logger.LogError(ex, "watsonx call failed for {Repo}", repositoryUrl);
            throw;
        }

        _logger.LogDebug("Raw watsonx output length: {Len}", rawOutput.Length);

        return ParseResponse(rawOutput, repositoryUrl, branch, context);
    }

    // ── Prompt builder ────────────────────────────────────────────────────────

    private static string BuildPrompt(
        string repositoryUrl,
        string branch,
        GitHubRepoContext? context)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are a senior software engineer performing a security and code-quality audit of a GitHub repository.");
        sb.AppendLine();
        sb.AppendLine($"Repository: {repositoryUrl}");
        sb.AppendLine($"Branch: {branch}");

        if (context is not null)
        {
            // ── Structured repo metadata ──────────────────────────────────────
            sb.AppendLine();
            sb.AppendLine("## Repository Metadata (fetched from GitHub)");
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(context.Description))
                sb.AppendLine($"Description: {context.Description}");
            if (!string.IsNullOrWhiteSpace(context.PrimaryLanguage))
                sb.AppendLine($"Primary language (GitHub): {context.PrimaryLanguage}");

            if (context.LanguageCounts.Any())
            {
                sb.AppendLine("Detected source languages (extension count):");
                foreach (var (lang, count) in context.LanguageCounts
                    .OrderByDescending(kv => kv.Value))
                {
                    sb.AppendLine($"  - {lang}: {count} file(s)");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"Total files in tree: {context.TotalFileCount}");
            sb.AppendLine($"Files sampled for this analysis: {context.SampledFileCount}");
            sb.AppendLine($"Has README: {context.HasReadme}");
            sb.AppendLine($"Has test files: {context.HasTests}");
            sb.AppendLine($"Has CI configuration: {context.HasCiConfig}");

            if (context.DependencyFiles.Any())
                sb.AppendLine($"Dependency manifests: {string.Join(", ", context.DependencyFiles)}");
            if (context.ConfigFiles.Any())
                sb.AppendLine($"Config files at root: {string.Join(", ", context.ConfigFiles)}");
            if (context.TopLevelDirectories.Any())
                sb.AppendLine($"Top-level directories: {string.Join(", ", context.TopLevelDirectories)}");

            // ── Sampled file content ──────────────────────────────────────────
            if (context.SampledFiles.Any())
            {
                sb.AppendLine();
                sb.AppendLine("## Sampled Source Files");
                sb.AppendLine("(Content is truncated for context safety. Focus on patterns, not completeness.)");
                sb.AppendLine();

                foreach (var file in context.SampledFiles)
                {
                    sb.AppendLine($"### {file.Path}");
                    sb.AppendLine("```");
                    sb.AppendLine(file.Content);
                    sb.AppendLine("```");
                    sb.AppendLine();
                }
            }

            // ── Known file paths for validation ───────────────────────────────
            var knownPaths = context.SampledFiles.Select(f => f.Path).ToList();
            knownPaths.AddRange(context.DependencyFiles);
            knownPaths.AddRange(context.ConfigFiles);
            if (knownPaths.Any())
            {
                sb.AppendLine("## Known File Paths in Repository (validated paths — only use these in affectedPath fields)");
                foreach (var p in knownPaths.Distinct())
                    sb.AppendLine($"  - {p}");
            }
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("Note: No live repository inspection was performed. " +
                          "Base your analysis on your knowledge of the repository " +
                          "and general best practices for the detected technology stack.");
        }

        // ── JSON schema instruction ───────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("Analyse the repository and respond with ONLY a single valid JSON object — no markdown, no explanation, no code fences.");
        sb.AppendLine();
        sb.AppendLine("The JSON must conform EXACTLY to this schema:");
        sb.AppendLine("""
            {
              "scores": {
                "overall": <int 0-100>,
                "codeQuality": <int 0-100>,
                "security": <int 0-100>,
                "performance": <int 0-100>,
                "testing": <int 0-100>,
                "documentation": <int 0-100>
              },
              "findings": {
                "critical": <int>,
                "high": <int>,
                "medium": <int>,
                "low": <int>
              },
              "recommendations": [
                {
                  "id": "rec-1",
                  "severity": "<critical|high|medium|low>",
                  "category": "<Security|Code Quality|Performance|Testing|Documentation|Maintainability|CI/CD|Dependencies>",
                  "title": "<short title>",
                  "explanation": "<why this matters — be specific about observed patterns>",
                  "evidence": "<specific evidence from the sampled files, or 'Evidence unavailable' if none>",
                  "recommendedAction": "<concrete step the developer should take>",
                  "affectedPath": "<exact path from the known file paths list, or null if not applicable>"
                }
              ],
              "nextActions": [
                {
                  "priority": <int starting at 1>,
                  "action": "<short action title>",
                  "rationale": "<reason — be specific about evidence from the repository>"
                }
              ]
            }
            """);
        sb.AppendLine();
        sb.AppendLine("Rules:");
        sb.AppendLine("- Provide at least 3 recommendations covering different categories.");
        sb.AppendLine("- Provide at least 3 next actions.");
        sb.AppendLine("- Only reference paths that appear in the 'Known File Paths' list above. Do NOT invent file paths.");
        sb.AppendLine("- If no evidence exists for a finding, set evidence to 'Evidence unavailable'.");
        sb.AppendLine("- Do NOT flag issues for which you have no evidence from the repository.");
        sb.AppendLine("- overall score should reflect the average of the five dimension scores.");
        sb.AppendLine("- Respond with the JSON object only. Do NOT include any other text.");

        return sb.ToString();
    }

    // ── Response parser ───────────────────────────────────────────────────────

    private AnalysisResponseDto ParseResponse(
        string rawOutput,
        string repositoryUrl,
        string branch,
        GitHubRepoContext? context)
    {
        // Locate the JSON object inside the raw model output.
        var json = ExtractJsonObject(rawOutput);

        WatsonxAnalysisPayload? payload = null;
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                payload = JsonSerializer.Deserialize<WatsonxAnalysisPayload>(json, JsonOpts);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to deserialise watsonx JSON response.");
            }
        }

        if (payload is null)
        {
            _logger.LogWarning(
                "watsonx returned unparseable output; falling back to minimal response. Raw: {Raw}",
                rawOutput.Length > 500 ? rawOutput[..500] : rawOutput);

            return BuildFallbackResponse(repositoryUrl, branch);
        }

        var repoName = ExtractRepoName(repositoryUrl);

        // Build the set of known paths for validation
        var knownPaths = BuildKnownPaths(context);

        var scores = new ScoresDto(
            Overall       : Clamp(payload.Scores?.Overall       ?? 50),
            CodeQuality   : Clamp(payload.Scores?.CodeQuality   ?? 50),
            Security      : Clamp(payload.Scores?.Security      ?? 50),
            Performance   : Clamp(payload.Scores?.Performance   ?? 50),
            Testing       : Clamp(payload.Scores?.Testing       ?? 50),
            Documentation : Clamp(payload.Scores?.Documentation ?? 50));

        var findings = new FindingsSummaryDto(
            Critical : Math.Max(0, payload.Findings?.Critical ?? 0),
            High     : Math.Max(0, payload.Findings?.High     ?? 0),
            Medium   : Math.Max(0, payload.Findings?.Medium   ?? 0),
            Low      : Math.Max(0, payload.Findings?.Low      ?? 0));

        var recommendations = (payload.Recommendations ?? [])
            .Select((r, i) => new RecommendationDto(
                Id              : string.IsNullOrWhiteSpace(r.Id) ? $"rec-{i + 1}" : r.Id,
                Severity        : NormaliseSeverity(r.Severity),
                Category        : r.Category        ?? "General",
                Title           : r.Title           ?? "Finding",
                Explanation     : r.Explanation     ?? string.Empty,
                Evidence        : string.IsNullOrWhiteSpace(r.Evidence)
                                    ? "Evidence unavailable"
                                    : r.Evidence,
                RecommendedAction: r.RecommendedAction ?? string.Empty,
                AffectedPath    : ValidatePath(r.AffectedPath, knownPaths),
                Line            : null))
            .ToList();

        var nextActions = (payload.NextActions ?? [])
            .Select((a, i) => new NextActionDto(
                Priority  : a.Priority > 0 ? a.Priority : i + 1,
                Action    : a.Action   ?? $"Action {i + 1}",
                Rationale : a.Rationale ?? string.Empty))
            .ToList();

        RepoMetadataDto? metadata = context is null ? null : new RepoMetadataDto(
            Description        : context.Description,
            PrimaryLanguage    : context.PrimaryLanguage,
            LanguageCounts     : context.LanguageCounts,
            TotalFileCount     : context.TotalFileCount,
            SampledFileCount   : context.SampledFileCount,
            HasReadme          : context.HasReadme,
            HasTests           : context.HasTests,
            HasCiConfig        : context.HasCiConfig,
            DependencyFiles    : context.DependencyFiles,
            ConfigFiles        : context.ConfigFiles,
            TopLevelDirectories: context.TopLevelDirectories);

        return new AnalysisResponseDto(
            Repository     : new RepositoryInfoDto(repositoryUrl, repoName, branch),
            Scores         : scores,
            Findings       : findings,
            Recommendations: recommendations,
            NextActions    : nextActions,
            AnalyzedAt     : DateTime.UtcNow,
            AnalysisSource : "Watsonx",
            RepoMetadata   : metadata);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Extracts the first complete JSON object from model output.
    /// Works even when the model wraps the JSON in markdown code fences.
    /// </summary>
    private static string? ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        if (start < 0) return null;

        var depth = 0;
        for (var i = start; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '{': depth++; break;
                case '}':
                    depth--;
                    if (depth == 0)
                        return text[start..(i + 1)];
                    break;
            }
        }
        return null;
    }

    private static string ExtractRepoName(string url)
    {
        var trimmed   = url.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        return lastSlash >= 0 && lastSlash < trimmed.Length - 1
            ? trimmed[(lastSlash + 1)..]
            : trimmed;
    }

    private static int Clamp(int value) =>
        Math.Max(0, Math.Min(100, value));

    private static string NormaliseSeverity(string? severity) =>
        (severity?.ToLowerInvariant()) switch
        {
            "critical" => "critical",
            "high"     => "high",
            "medium"   => "medium",
            "low"      => "low",
            _          => "medium",
        };

    /// <summary>
    /// Builds a hash set of known file paths from the repository context.
    /// Used to validate that model-referenced paths actually exist.
    /// </summary>
    private static HashSet<string> BuildKnownPaths(GitHubRepoContext? context)
    {
        if (context is null)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in context.SampledFiles)      set.Add(f.Path);
        foreach (var f in context.DependencyFiles)   set.Add(f);
        foreach (var f in context.ConfigFiles)       set.Add(f);
        return set;
    }

    /// <summary>
    /// Returns <paramref name="path"/> when it is known to exist in the repository context,
    /// otherwise returns null. When context is unavailable (no live GitHub inspection),
    /// any path is accepted as-is to allow URL-only analysis.
    /// </summary>
    private static string? ValidatePath(string? path, HashSet<string> knownPaths)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        // No context available → accept model-supplied paths (best-effort, no validation)
        if (knownPaths.Count == 0) return path;

        // Exact match or prefix match (model may supply a directory rather than a file)
        if (knownPaths.Contains(path)) return path;

        // Accept if any known path starts with the supplied prefix
        foreach (var known in knownPaths)
        {
            if (known.StartsWith(path.TrimEnd('/'), StringComparison.OrdinalIgnoreCase))
                return path;
        }

        // Path not found in repository — discard it
        return null;
    }

    private static AnalysisResponseDto BuildFallbackResponse(
        string repositoryUrl, string branch) =>
        new(
            Repository    : new RepositoryInfoDto(repositoryUrl, ExtractRepoName(repositoryUrl), branch),
            Scores        : new ScoresDto(0, 0, 0, 0, 0, 0),
            Findings      : new FindingsSummaryDto(0, 0, 0, 0),
            Recommendations: [
                new RecommendationDto(
                    "rec-1", "medium", "General",
                    "Analysis unavailable",
                    "The AI model returned an unexpected response.",
                    "Evidence unavailable",
                    "Please try again or switch to Mock mode for offline testing.",
                    null, null)
            ],
            NextActions   : [
                new NextActionDto(1, "Retry analysis", "The previous attempt produced an unexpected response.")
            ],
            AnalyzedAt    : DateTime.UtcNow,
            AnalysisSource: "Watsonx");

    // ── Internal DTOs for deserialising the model's JSON output ──────────────

    private sealed class WatsonxAnalysisPayload
    {
        public WxScores?               Scores          { get; set; }
        public WxFindings?             Findings        { get; set; }
        public List<WxRecommendation>? Recommendations { get; set; }
        public List<WxNextAction>?     NextActions     { get; set; }
    }

    private sealed class WxScores
    {
        public int Overall       { get; set; }
        public int CodeQuality   { get; set; }
        public int Security      { get; set; }
        public int Performance   { get; set; }
        public int Testing       { get; set; }
        public int Documentation { get; set; }
    }

    private sealed class WxFindings
    {
        public int Critical { get; set; }
        public int High     { get; set; }
        public int Medium   { get; set; }
        public int Low      { get; set; }
    }

    private sealed class WxRecommendation
    {
        public string? Id               { get; set; }
        public string? Severity         { get; set; }
        public string? Category         { get; set; }
        public string? Title            { get; set; }
        public string? Explanation      { get; set; }
        public string? Evidence         { get; set; }
        public string? RecommendedAction { get; set; }
        public string? AffectedPath     { get; set; }
        // Legacy field support: also accept "description" from older prompts
        [JsonPropertyName("description")]
        public string? Description      { get; set; }
    }

    private sealed class WxNextAction
    {
        public int     Priority  { get; set; }
        public string? Action    { get; set; }
        public string? Rationale { get; set; }
    }
}
