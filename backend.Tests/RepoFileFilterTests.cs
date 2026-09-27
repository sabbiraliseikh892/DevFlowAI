using DevFlowAI.API.Services.GitHub;

namespace DevFlowAI.Tests;

/// <summary>
/// Tests for <see cref="RepoFileFilter"/> — covers directory exclusion,
/// file exclusion, CI / dependency / test detection, and language mapping.
/// </summary>
public class RepoFileFilterTests
{
    // ── IsIgnoredDirectory ────────────────────────────────────────────────────

    [Theory]
    [InlineData("node_modules")]
    [InlineData("bin")]
    [InlineData("obj")]
    [InlineData("dist")]
    [InlineData("build")]
    [InlineData(".git")]
    [InlineData("vendor")]
    [InlineData("__pycache__")]
    [InlineData(".venv")]
    [InlineData("coverage")]
    [InlineData("target")]
    [InlineData(".next")]
    [InlineData("wwwroot")]
    public void IsIgnoredDirectory_ReturnsTrue_ForKnownIgnoredDirs(string dir)
    {
        Assert.True(RepoFileFilter.IsIgnoredDirectory(dir));
    }

    [Theory]
    [InlineData("src")]
    [InlineData("lib")]
    [InlineData("app")]
    [InlineData("tests")]
    [InlineData("docs")]
    [InlineData("api")]
    public void IsIgnoredDirectory_ReturnsFalse_ForAllowedDirs(string dir)
    {
        Assert.False(RepoFileFilter.IsIgnoredDirectory(dir));
    }

    [Fact]
    public void IsIgnoredDirectory_ReturnsTrue_WhenIgnoredDirIsNestedInPath()
    {
        Assert.True(RepoFileFilter.IsIgnoredDirectory("src/node_modules"));
        Assert.True(RepoFileFilter.IsIgnoredDirectory("packages/app/dist"));
    }

    [Fact]
    public void IsIgnoredDirectory_ReturnsFalse_ForEmptyString()
    {
        Assert.False(RepoFileFilter.IsIgnoredDirectory(""));
    }

    // ── IsExcluded ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("node_modules/lodash/index.js")]
    [InlineData("bin/Release/app.dll")]
    [InlineData("obj/Debug/app.o")]
    [InlineData("dist/bundle.js")]
    [InlineData("build/output.js")]
    [InlineData(".git/HEAD")]
    public void IsExcluded_ReturnsTrue_ForFilesInIgnoredDirectories(string path)
    {
        Assert.True(RepoFileFilter.IsExcluded(path));
    }

    [Theory]
    [InlineData(".env")]
    [InlineData("config/.env")]
    [InlineData("id_rsa")]
    [InlineData("credentials.json")]
    [InlineData("secrets.yaml")]
    [InlineData("serviceAccountKey.json")]
    public void IsExcluded_ReturnsTrue_ForSensitiveFiles(string path)
    {
        Assert.True(RepoFileFilter.IsExcluded(path));
    }

    [Theory]
    [InlineData("src/main.cs")]
    [InlineData("app/index.ts")]
    [InlineData("lib/utils.js")]
    [InlineData("backend/Program.cs")]
    [InlineData("README.md")]
    [InlineData("package.json")]
    [InlineData("Dockerfile")]
    [InlineData(".gitignore")]
    public void IsExcluded_ReturnsFalse_ForAllowedSourceFiles(string path)
    {
        Assert.False(RepoFileFilter.IsExcluded(path));
    }

    [Theory]
    [InlineData("photo.png")]
    [InlineData("logo.jpg")]
    [InlineData("data.csv")]
    [InlineData("archive.zip")]
    [InlineData("binary.exe")]
    public void IsExcluded_ReturnsTrue_ForBinaryAndDataFiles(string path)
    {
        Assert.True(RepoFileFilter.IsExcluded(path));
    }

    [Fact]
    public void IsExcluded_ReturnsFalse_ForEnvExample()
    {
        // .env.example is OK (no secrets, documents expected variables)
        Assert.False(RepoFileFilter.IsExcluded(".env.example"));
    }

    [Fact]
    public void IsExcluded_ReturnsTrue_ForEmptyPath()
    {
        Assert.True(RepoFileFilter.IsExcluded(""));
    }

    // ── IsCiFile ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(".github/workflows/ci.yml")]
    [InlineData(".travis.yml")]
    [InlineData(".circleci/config.yml")]
    [InlineData("azure-pipelines.yml")]
    [InlineData(".gitlab-ci.yml")]
    [InlineData("Jenkinsfile")]
    public void IsCiFile_ReturnsTrue_ForKnownCiFiles(string path)
    {
        Assert.True(RepoFileFilter.IsCiFile(path));
    }

    [Theory]
    [InlineData("src/app.ts")]
    [InlineData("README.md")]
    [InlineData("package.json")]
    public void IsCiFile_ReturnsFalse_ForNonCiFiles(string path)
    {
        Assert.False(RepoFileFilter.IsCiFile(path));
    }

    // ── IsDependencyFile ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("package.json")]
    [InlineData("requirements.txt")]
    [InlineData("Gemfile")]
    [InlineData("go.mod")]
    [InlineData("Cargo.toml")]
    [InlineData("pom.xml")]
    [InlineData("MyApp.csproj")]
    [InlineData("Library.fsproj")]
    public void IsDependencyFile_ReturnsTrue_ForKnownDependencyManifests(string path)
    {
        Assert.True(RepoFileFilter.IsDependencyFile(path));
    }

    [Theory]
    [InlineData("src/App.tsx")]
    [InlineData("README.md")]
    [InlineData(".gitignore")]
    public void IsDependencyFile_ReturnsFalse_ForNonDependencyFiles(string path)
    {
        Assert.False(RepoFileFilter.IsDependencyFile(path));
    }

    // ── IsTestFile ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("src/test/MyTest.java")]
    [InlineData("tests/unit/parser.test.ts")]
    [InlineData("spec/user_spec.rb")]
    [InlineData("__tests__/App.test.js")]
    [InlineData("AppTests.cs")]              // ends with "Tests"
    [InlineData("backend.Tests/MyTest.cs")] // .Tests in path
    [InlineData("Calculator.spec.ts")]
    public void IsTestFile_ReturnsTrue_ForCommonTestConventions(string path)
    {
        Assert.True(RepoFileFilter.IsTestFile(path));
    }

    [Theory]
    [InlineData("src/app.ts")]
    [InlineData("lib/utils.py")]
    [InlineData("README.md")]
    public void IsTestFile_ReturnsFalse_ForNonTestFiles(string path)
    {
        Assert.False(RepoFileFilter.IsTestFile(path));
    }

    // ── ExtensionToLanguage ───────────────────────────────────────────────────

    [Theory]
    [InlineData(".cs",  "C#")]
    [InlineData(".ts",  "TypeScript")]
    [InlineData(".tsx", "TypeScript")]
    [InlineData(".js",  "JavaScript")]
    [InlineData(".py",  "Python")]
    [InlineData(".go",  "Go")]
    [InlineData(".rs",  "Rust")]
    [InlineData(".java","Java")]
    [InlineData(".kt",  "Kotlin")]
    public void ExtensionToLanguage_ReturnsCorrectLanguage(string ext, string expected)
    {
        Assert.Equal(expected, RepoFileFilter.ExtensionToLanguage(ext));
    }

    [Theory]
    [InlineData(".json")]
    [InlineData(".md")]
    [InlineData(".yml")]
    [InlineData(".png")]
    public void ExtensionToLanguage_ReturnsNull_ForNonSourceExtensions(string ext)
    {
        Assert.Null(RepoFileFilter.ExtensionToLanguage(ext));
    }

    // ── Budget constants ──────────────────────────────────────────────────────

    [Fact]
    public void MaxSampledFiles_IsAtLeast10()
    {
        Assert.True(RepoFileFilter.MaxSampledFiles >= 10,
            "MaxSampledFiles should allow at least 10 files for meaningful analysis");
    }

    [Fact]
    public void MaxTotalContextChars_IsAtLeast20000()
    {
        Assert.True(RepoFileFilter.MaxTotalContextChars >= 20_000,
            "MaxTotalContextChars should allow enough content for the AI model");
    }
}
