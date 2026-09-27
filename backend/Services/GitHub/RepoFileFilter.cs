namespace DevFlowAI.API.Services.GitHub;

/// <summary>
/// Decides which repository files are safe and useful to include in the
/// AI analysis context.
///
/// Design goals:
///  - Exclude generated artefacts and dependencies (node_modules, bin, obj …)
///  - Exclude sensitive files (.env, secrets, certificates)
///  - Exclude binary and data files
///  - Limit the total context to a manageable size
/// </summary>
public static class RepoFileFilter
{
    // ── Directories to skip entirely ─────────────────────────────────────────

    private static readonly HashSet<string> IgnoredDirectories = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "node_modules", ".node_modules",
        "bin", "obj",
        "dist", "build", "out", "output",
        ".git", ".github_pages",
        "vendor",                       // PHP / Go
        "__pycache__", ".pytest_cache",
        ".tox", ".venv", "venv", "env",
        "coverage", ".nyc_output",
        ".cache", ".parcel-cache",
        "target",                       // Rust / Maven
        "packages",                     // some monorepos
        "bower_components",
        ".svn", ".hg",
        "generated", "gen",
        "migrations",                   // DB migration dumps can be huge
        ".angular",
        ".next", ".nuxt",
        "storybook-static",
        "public",                       // often just static assets
        "assets",
        "wwwroot",                      // ASP.NET static files
    };

    // ── File extensions that are allowed ─────────────────────────────────────
    // (all other extensions are treated as binary / irrelevant)

    private static readonly HashSet<string> AllowedExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        // Source code
        ".cs", ".fs", ".vb",            // .NET
        ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs",
        ".py", ".rb", ".php",
        ".java", ".kt", ".scala",
        ".go", ".rs",
        ".cpp", ".c", ".h", ".cc", ".hh",
        ".swift",
        ".dart",
        ".ex", ".exs",                  // Elixir
        ".clj", ".cljs",               // Clojure
        ".hs",                          // Haskell
        // Config / build
        ".json", ".yaml", ".yml", ".toml", ".xml",
        ".csproj", ".fsproj", ".vbproj", ".sln",
        ".gradle",
        ".tf",                          // Terraform
        ".bicep",
        // Web
        ".html", ".htm", ".css", ".scss", ".sass", ".less",
        // Docs
        ".md", ".rst", ".txt",
        // Shell / scripts
        ".sh", ".bash", ".zsh", ".ps1", ".psm1", ".bat", ".cmd",
        // Lock files (structure info, not content)
        ".lock",
        // Misc
        ".gitignore", ".gitattributes",
        ".editorconfig",
        ".eslintrc", ".prettierrc",
        ".env.example",                 // example env is OK; .env itself is not
    };

    // ── Filenames (without extension) that should always be skipped ───────────

    private static readonly HashSet<string> BlockedFileNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".env.local", ".env.development", ".env.production",
        ".env.test", ".env.staging",
        "id_rsa", "id_ecdsa", "id_ed25519",
        "id_rsa.pub", "id_ecdsa.pub",
        "secrets.yaml", "secrets.yml", "secrets.json",
        "credentials", "credentials.json",
        "serviceAccountKey.json",
        ".npmrc",                       // may contain auth tokens
        ".pypirc",
        ".netrc",
    };

    // ── File names whose content is always sampled if present ────────────────
    // (regardless of extension, because some have no extension)

    private static readonly HashSet<string> PriorityFileNames = new(
        StringComparer.OrdinalIgnoreCase)
    {
        "README.md", "README", "README.txt", "README.rst",
        ".gitignore",
        "Makefile", "Dockerfile", "docker-compose.yml", "docker-compose.yaml",
        "package.json", "package-lock.json", "yarn.lock", "pnpm-lock.yaml",
        "requirements.txt", "Pipfile", "poetry.lock", "setup.py", "pyproject.toml",
        "Gemfile", "Gemfile.lock",
        "go.mod",
        "Cargo.toml",
        "pom.xml",
        ".github",                      // directory marker
        "CODEOWNERS",
    };

    // ── Context budget ────────────────────────────────────────────────────────

    /// <summary>Maximum number of files to sample into the AI prompt.</summary>
    public const int MaxSampledFiles = 20;

    /// <summary>Maximum total character budget across all sampled files.</summary>
    public const int MaxTotalContextChars = 40_000;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the directory at the given path segment should be skipped.
    /// Checks any component of the path — e.g. "src/node_modules" is also ignored.
    /// </summary>
    public static bool IsIgnoredDirectory(string dirPath)
    {
        if (string.IsNullOrWhiteSpace(dirPath)) return false;

        foreach (var segment in dirPath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (IgnoredDirectories.Contains(segment))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true if the file at <paramref name="filePath"/> should be excluded
    /// from the AI context.
    /// </summary>
    public static bool IsExcluded(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return true;

        // Check every directory component
        var normalized = filePath.Replace('\\', '/');
        var parts = normalized.Split('/');

        // Skip if any parent directory is blocked
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (IgnoredDirectories.Contains(parts[i]))
                return true;
        }

        var fileName  = parts[^1];
        var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
        var ext       = Path.GetExtension(fileName);

        // Always allow .env.example (documents expected variables, no real secrets)
        if (fileName.Equals(".env.example", StringComparison.OrdinalIgnoreCase))
            return false;

        // Blocked file names (whole name or name without extension)
        if (BlockedFileNames.Contains(fileName))  return true;
        if (BlockedFileNames.Contains(nameNoExt)) return true;

        // Files with no extension: allow only known names, block the rest
        if (string.IsNullOrEmpty(ext))
        {
            return !PriorityFileNames.Contains(fileName);
        }

        return !AllowedExtensions.Contains(ext);
    }

    /// <summary>
    /// Detects common CI configuration file paths.
    /// </summary>
    public static bool IsCiFile(string filePath)
    {
        var n = filePath.Replace('\\', '/').ToLowerInvariant();
        return n.StartsWith(".github/workflows/")
            || n == ".travis.yml"
            || n == "circle.ci"
            || n.StartsWith(".circleci/")
            || n == "jenkinsfile"
            || n.StartsWith("azure-pipelines")
            || n == ".gitlab-ci.yml"
            || n == "bitbucket-pipelines.yml"
            || n.StartsWith(".buildkite/");
    }

    /// <summary>
    /// Detects dependency manifest files.
    /// </summary>
    public static bool IsDependencyFile(string filePath)
    {
        var n = Path.GetFileName(filePath).ToLowerInvariant();
        return n is "package.json" or "requirements.txt" or "pipfile"
            or "gemfile" or "go.mod" or "cargo.toml" or "pom.xml"
            or "build.gradle" or "build.gradle.kts" or "pyproject.toml"
            or "setup.py"
            || Path.GetExtension(n) is ".csproj" or ".fsproj" or ".vbproj";
    }

    /// <summary>
    /// Detects test files by path / name conventions.
    /// </summary>
    public static bool IsTestFile(string filePath)
    {
        var n = filePath.Replace('\\', '/').ToLowerInvariant();

        // Check each directory segment — handles paths starting with "spec/..."
        var segments = n.Split('/');
        for (var i = 0; i < segments.Length - 1; i++)
        {
            var seg = segments[i];
            if (seg is "test" or "tests" or "spec" or "specs" or "__tests__")
                return true;
        }

        // Path-substring checks (handles paths like "src/test/MyTest.java")
        if (n.Contains("/test/") || n.Contains("/tests/")
            || n.Contains("/spec/") || n.Contains("/specs/")
            || n.Contains("/__tests__/"))
            return true;

        var nameNoExt = Path.GetFileNameWithoutExtension(segments[^1]);
        return nameNoExt.EndsWith(".test")
            || nameNoExt.EndsWith(".spec")
            || nameNoExt.EndsWith("test")
            || nameNoExt.EndsWith("tests")
            || n.Contains(".tests/");   // C# xUnit project convention: backend.Tests/
    }

    /// <summary>
    /// Returns the programming language name for a given file extension.
    /// Returns null for non-source files.
    /// </summary>
    public static string? ExtensionToLanguage(string ext) =>
        ext.ToLowerInvariant() switch
        {
            ".cs"  or ".csx"          => "C#",
            ".fs"  or ".fsx"          => "F#",
            ".vb"                     => "VB.NET",
            ".ts"  or ".tsx"          => "TypeScript",
            ".js"  or ".jsx" or ".mjs"=> "JavaScript",
            ".py"                     => "Python",
            ".rb"                     => "Ruby",
            ".php"                    => "PHP",
            ".java"                   => "Java",
            ".kt"  or ".kts"          => "Kotlin",
            ".scala"                  => "Scala",
            ".go"                     => "Go",
            ".rs"                     => "Rust",
            ".cpp" or ".cc" or ".cxx" => "C++",
            ".c"                      => "C",
            ".h"   or ".hh"           => "C/C++ Header",
            ".swift"                  => "Swift",
            ".dart"                   => "Dart",
            ".ex"  or ".exs"          => "Elixir",
            ".clj" or ".cljs"         => "Clojure",
            ".hs"                     => "Haskell",
            _                         => null,
        };
}
