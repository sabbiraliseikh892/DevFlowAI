using DevFlowAI.API.DTOs;
using DevFlowAI.API.Services.GitHub;

namespace DevFlowAI.API.Services;

/// <summary>
/// Deterministic mock implementation of IAnalysisService.
/// Produces realistic-looking analysis results derived from the repository URL
/// so the UI can be fully exercised without external AI credentials.
///
/// Replace with a real LLM-backed service (e.g. WatsonxAnalysisService)
/// by binding a different concrete type in Program.cs — no other changes required.
///
/// Phase 7: Now produces structured findings with Explanation / Evidence /
/// RecommendedAction / AffectedPath matching the full finding schema.
/// </summary>
public class MockAnalysisService : IAnalysisService
{
    public Task<AnalysisResponseDto> AnalyzeAsync(
        string repositoryUrl,
        string branch,
        GitHubRepoContext? context = null,
        CancellationToken ct = default)
    {
        // Derive a stable seed from the URL so the same repo always returns the same scores.
        int seed = Math.Abs(repositoryUrl.GetHashCode());
        var rng = new Random(seed);

        string repoName = ExtractRepoName(repositoryUrl);

        int codeQuality   = Clamp(rng.Next(55, 96),  55, 100);
        int security      = Clamp(rng.Next(48, 92),  48, 100);
        int performance   = Clamp(rng.Next(60, 98),  60, 100);
        int testing       = Clamp(rng.Next(40, 90),  40, 100);
        int documentation = Clamp(rng.Next(45, 92),  45, 100);
        int overall       = (codeQuality + security + performance + testing + documentation) / 5;

        var scores = new ScoresDto(overall, codeQuality, security, performance, testing, documentation);

        var findings = new FindingsSummaryDto(
            Critical : security < 55 ? 1 : 0,
            High     : security < 70 ? 2 : 1,
            Medium   : 3,
            Low      : 5
        );

        IReadOnlyList<RecommendationDto> recommendations =
            BuildRecommendations(codeQuality, security, performance, testing, documentation);

        var nextActions = BuildNextActions(security, testing);

        // When real repo context is available, enrich findings with actual data.
        if (context is not null)
        {
            if (!context.HasTests)
            {
                var existing = recommendations as List<RecommendationDto>
                    ?? recommendations.ToList();
                existing.Insert(0, new RecommendationDto(
                    "rec-ctx-1", "high", "Testing",
                    "No test files detected",
                    "The repository does not appear to contain any test files. " +
                    "Untested code is high-risk for regressions during refactors or dependency upgrades.",
                    "GitHub tree inspection found no test directories (test/, tests/, __tests__, spec/, etc.).",
                    "Add a test framework (e.g. Jest, pytest, xUnit) and write unit tests for core business logic.",
                    null, null));
                recommendations = existing;
            }

            if (!context.HasCiConfig)
            {
                var existing = recommendations as List<RecommendationDto>
                    ?? recommendations.ToList();
                existing.Add(new RecommendationDto(
                    "rec-ctx-2", "medium", "CI/CD",
                    "No CI configuration detected",
                    "Without automated builds and tests on every commit, regressions may go undetected until production.",
                    "No CI configuration files found (.github/workflows/, .travis.yml, .circleci/, etc.).",
                    "Add a CI workflow to automate builds, tests, and code quality checks on every pull request.",
                    null, null));
                recommendations = existing;
            }

            if (!context.HasReadme)
            {
                var existing = recommendations as List<RecommendationDto>
                    ?? recommendations.ToList();
                existing.Add(new RecommendationDto(
                    "rec-ctx-3", "low", "Documentation",
                    "README not found",
                    "The repository has no README file, making it harder for contributors to understand the project purpose and setup.",
                    "GitHub tree inspection found no README.md or README.rst at the repository root.",
                    "Add a README.md describing the project purpose, how to build, how to test, and how to contribute.",
                    null, null));
                recommendations = existing;
            }
        }

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

        var response = new AnalysisResponseDto(
            Repository    : new RepositoryInfoDto(repositoryUrl, repoName, branch),
            Scores        : scores,
            Findings      : findings,
            Recommendations: recommendations,
            NextActions   : nextActions,
            AnalyzedAt    : DateTime.UtcNow,
            AnalysisSource: "Mock",
            RepoMetadata  : metadata
        );

        return Task.FromResult(response);
    }

    // ── helpers ────────────────────────────────────────────────────────────────

    private static string ExtractRepoName(string url)
    {
        // Handle "https://github.com/owner/repo", "owner/repo", or bare "repo"
        var trimmed = url.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash >= 0 && lastSlash < trimmed.Length - 1)
            return trimmed[(lastSlash + 1)..];
        return trimmed;
    }

    private static int Clamp(int value, int min, int max) =>
        Math.Max(min, Math.Min(max, value));

    private static IReadOnlyList<RecommendationDto> BuildRecommendations(
        int codeQuality, int security, int performance, int testing, int documentation)
    {
        var list = new List<RecommendationDto>();
        int id = 1;

        if (security < 70)
        {
            list.Add(new RecommendationDto(
                $"rec-{id++}", "high", "Security",
                "Dependency with known vulnerability",
                "One or more third-party dependencies have publicly disclosed CVEs, which could be exploited by attackers.",
                "Static analysis of dependency manifests flagged packages with known CVEs.",
                "Run `npm audit` / `dotnet list package --vulnerable` and upgrade affected packages to their latest patched versions.",
                "package.json", null));
        }

        if (security < 80)
        {
            list.Add(new RecommendationDto(
                $"rec-{id++}", "high", "Security",
                "Potential secrets in source code",
                "Hardcoded credentials or API tokens in source code can be extracted by anyone with repository access.",
                "Static analysis flagged patterns consistent with hardcoded credentials or API tokens.",
                "Move all secrets to environment variables or a secrets manager. Add a pre-commit hook to block future secret commits.",
                "src/", null));
        }

        if (codeQuality < 75)
        {
            list.Add(new RecommendationDto(
                $"rec-{id++}", "medium", "Code Quality",
                "Excessive cyclomatic complexity",
                "Functions with high cyclomatic complexity are difficult to test, review, and maintain.",
                "Several functions exceed a cyclomatic complexity of 10, indicating deep nesting or many branches.",
                "Refactor complex functions by extracting smaller helper functions. Aim for cyclomatic complexity ≤ 10 per function.",
                null, null));
        }

        if (performance < 75)
        {
            list.Add(new RecommendationDto(
                $"rec-{id++}", "medium", "Performance",
                "N+1 query pattern detected",
                "Database queries inside loops result in O(n) round-trips, causing latency that scales with data volume.",
                "Database queries were found inside loops in the repository data access layer.",
                "Batch queries or use eager-loading (e.g. JOIN, Include()) to reduce database round-trips.",
                "src/repositories/", null));
        }

        if (testing < 60)
        {
            list.Add(new RecommendationDto(
                $"rec-{id++}", "medium", "Testing",
                "Low test coverage",
                "Low test coverage means regressions are likely to go undetected, increasing production risk.",
                "Estimated test coverage is below 60%. Core business logic paths appear untested.",
                "Add unit tests for core business logic. Prioritise edge cases and error paths. Target ≥ 70% coverage.",
                "tests/", null));
        }

        if (documentation < 65)
        {
            list.Add(new RecommendationDto(
                $"rec-{id++}", "low", "Documentation",
                "Public API lacks documentation",
                "Undocumented APIs increase onboarding time and the risk of incorrect usage.",
                "Public methods and classes lack documentation comments (JSDoc, XML docs, docstrings).",
                "Add documentation comments to all public APIs. Consider a documentation generator (e.g. Docusaurus, Swagger).",
                null, null));
        }

        list.Add(new RecommendationDto(
            $"rec-{id++}", "low", "Code Quality",
            "Inconsistent code style",
            "Formatting inconsistencies increase cognitive load during code review and contribute to merge conflicts.",
            "Multiple formatting styles observed across files.",
            "Enforce a shared formatter (Prettier, EditorConfig, dotnet-format) and add a pre-commit hook to auto-format.",
            null, null));

        return list;
    }

    private static IReadOnlyList<NextActionDto> BuildNextActions(int security, int testing)
    {
        var actions = new List<NextActionDto>
        {
            new(1,
                "Upgrade vulnerable dependencies",
                security < 70
                    ? "One or more dependencies have known CVEs — immediate action required."
                    : "Stay ahead of supply-chain risk by keeping dependencies current."),

            new(2,
                "Enable branch protection rules",
                "Require pull-request reviews and passing CI checks before merging to the default branch."),

            new(3,
                "Add or expand automated tests",
                testing < 60
                    ? "Test coverage is critically low — add tests before the next release to avoid regressions."
                    : "Raise test coverage above 70% to catch regressions early and build confidence in refactors."),

            new(4,
                "Resolve high-priority findings",
                "Address all Critical and High findings in the analysis before the next release."),

            new(5,
                "Introduce static analysis in CI",
                "Configure a linter or SAST gate to prevent new security and quality issues from being merged.")
        };
        return actions;
    }
}
