using System.Text.Json;
using DevFlowAI.API.DTOs;
using DevFlowAI.API.Services.GitHub;
using DevFlowAI.API.Services.Watsonx;
using Microsoft.AspNetCore.Mvc;

namespace DevFlowAI.API.Controllers;

[ApiController]
[Route("api/code-review")]
public class CodeReviewController : ControllerBase
{
    private readonly IGitHubService _gitHubService;
    private readonly IWatsonxHttpClient _watsonxClient;

    public CodeReviewController(
        IGitHubService gitHubService,
        IWatsonxHttpClient watsonxClient)
    {
        _gitHubService = gitHubService;
        _watsonxClient = watsonxClient;
    }

    [HttpPost("review")]
    public async Task<IActionResult> Review(
        [FromBody] CodeReviewRequestDto request,
        CancellationToken ct)
    {
        if (request == null)
        {
            return BadRequest(new
            {
                message = "Request body is required."
            });
        }

        if (string.IsNullOrWhiteSpace(request.RepositoryUrl))
        {
            return BadRequest(new
            {
                message = "Repository URL is required."
            });
        }

        var github =
            ParseGitHubUrl(request.RepositoryUrl);

        if (github == null)
        {
            return BadRequest(new
            {
                message =
                    "Please enter a valid public GitHub repository URL."
            });
        }

        try
        {
            var branch =
                string.IsNullOrWhiteSpace(request.Branch)
                    ? null
                    : request.Branch.Trim();

            var context =
                await _gitHubService.GetRepoContextAsync(
                    github.Value.Owner,
                    github.Value.Repo,
                    branch,
                    ct);

            var prompt =
                BuildCodeReviewPrompt(context);

            var aiResponse =
                await _watsonxClient.GenerateTextAsync(
                    prompt,
                    ct);

            var findings =
                ExtractFindings(aiResponse);

            return Ok(new
            {
                repository = context.FullName,
                branch = context.Branch,
                source = "IBM watsonx.ai",
                findings = findings,
                review = aiResponse
            });
        }
        catch (GitHubServiceException ex)
        {
            return StatusCode(500, new
            {
                message = ex.Message
            });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new
            {
                message = "Code review was cancelled."
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Code review failed.",
                detail = ex.Message
            });
        }
    }

    private static (string Owner, string Repo)? ParseGitHubUrl(
        string repositoryUrl)
    {
        if (!Uri.TryCreate(
                repositoryUrl.Trim(),
                UriKind.Absolute,
                out var uri))
        {
            return null;
        }

        if (!uri.Host.Equals(
                "github.com",
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var parts =
            uri.AbsolutePath
                .Trim('/')
                .Split(
                    '/',
                    StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2)
        {
            return null;
        }

        var owner = parts[0];
        var repo = parts[1];

        if (repo.EndsWith(
                ".git",
                StringComparison.OrdinalIgnoreCase))
        {
            repo = repo[..^4];
        }

        return (
            string.IsNullOrWhiteSpace(owner)
                ? null
                : owner,
            string.IsNullOrWhiteSpace(repo)
                ? null
                : repo
        );
    }

    private static List<CodeReviewFinding> ExtractFindings(
        string aiResponse)
    {
        var result =
            new List<CodeReviewFinding>();

        if (string.IsNullOrWhiteSpace(aiResponse))
        {
            return result;
        }

        var json =
            ExtractFindingsJson(aiResponse);

        if (string.IsNullOrWhiteSpace(json))
        {
            return result;
        }

        try
        {
            using var document =
                JsonDocument.Parse(json);

            if (!document.RootElement.TryGetProperty(
                    "findings",
                    out var findingsElement))
            {
                return result;
            }

            if (findingsElement.ValueKind !=
                JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in
                     findingsElement.EnumerateArray())
            {
                var finding =
                    new CodeReviewFinding
                    {
                        Severity =
                            GetString(
                                item,
                                "severity",
                                "MEDIUM"),

                        Category =
                            GetString(
                                item,
                                "category",
                                "Code Quality"),

                        Title =
                            GetString(
                                item,
                                "title",
                                "Code issue detected"),

                        File =
                            GetString(
                                item,
                                "file",
                                "Unknown file"),

                        Line =
                            GetInt(
                                item,
                                "line"),

                        Description =
                            GetString(
                                item,
                                "description",
                                "No description provided."),

                        Recommendation =
                            GetString(
                                item,
                                "recommendation",
                                "Review and improve the code.")
                    };

                result.Add(finding);
            }
        }
        catch
        {
            return new List<CodeReviewFinding>();
        }

        return result;
    }

    private static string ExtractFindingsJson(
        string response)
    {
        var start =
            response.IndexOf(
                "{\"findings\"",
                StringComparison.OrdinalIgnoreCase);

        if (start < 0)
        {
            start =
                response.IndexOf(
                    "{\r\n\"findings\"",
                    StringComparison.OrdinalIgnoreCase);
        }

        if (start < 0)
        {
            start =
                response.IndexOf(
                    "{\n\"findings\"",
                    StringComparison.OrdinalIgnoreCase);
        }

        if (start < 0)
        {
            var findingsIndex =
                response.IndexOf(
                    "\"findings\"",
                    StringComparison.OrdinalIgnoreCase);

            if (findingsIndex >= 0)
            {
                start =
                    response.LastIndexOf(
                        '{',
                        findingsIndex);
            }
        }

        if (start < 0)
        {
            return string.Empty;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < response.Length; i++)
        {
            var c = response[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (c == '\\' && inString)
            {
                escaped = true;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
            {
                continue;
            }

            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return response.Substring(
                        start,
                        i - start + 1);
                }
            }
        }

        return string.Empty;
    }

    private static string GetString(
        JsonElement element,
        string property,
        string defaultValue)
    {
        if (!element.TryGetProperty(
                property,
                out var value))
        {
            return defaultValue;
        }

        if (value.ValueKind ==
            JsonValueKind.String)
        {
            return value.GetString() ??
                   defaultValue;
        }

        return value.ToString();
    }

    private static int GetInt(
        JsonElement element,
        string property)
    {
        if (!element.TryGetProperty(
                property,
                out var value))
        {
            return 0;
        }

        if (value.ValueKind ==
                JsonValueKind.Number &&
            value.TryGetInt32(out var number))
        {
            return number;
        }

        if (value.ValueKind ==
                JsonValueKind.String &&
            int.TryParse(
                value.GetString(),
                out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static string BuildCodeReviewPrompt(
        GitHubRepoContext context)
    {
        var files =
            string.Join(
                "\n\n",
                context.SampledFiles.Select(
                    x =>
                        "===== FILE: " +
                        x.Path +
                        " =====\n" +
                        x.Content));

        return
            "You are DevFlow AI, an expert code reviewer.\n\n" +

            "Review this GitHub repository and identify real " +
            "actionable issues.\n\n" +

            "Repository: " +
            context.FullName +
            "\n" +

            "Branch: " +
            context.Branch +
            "\n" +

            "Primary Language: " +
            (context.PrimaryLanguage ?? "Unknown") +
            "\n\n" +

            "Review areas:\n" +
            "- Security\n" +
            "- Code Quality\n" +
            "- Performance\n" +
            "- Maintainability\n" +
            "- Reliability\n" +
            "- Error Handling\n\n" +

            "IMPORTANT:\n" +
            "- Return maximum 5 findings.\n" +
            "- Only report issues supported by the supplied code.\n" +
            "- Do not invent files.\n" +
            "- Do not invent line numbers.\n" +
            "- If line number is unknown use 0.\n" +
            "- Do not return source code.\n" +
            "- Do not return Markdown.\n" +
            "- Do not include comments.\n" +
            "- Do not include text before or after JSON.\n\n" +

            "Return EXACTLY this JSON structure:\n\n" +

            "{\"findings\":[{\"severity\":\"MEDIUM\"," +
            "\"category\":\"Code Quality\"," +
            "\"title\":\"Issue title\"," +
            "\"file\":\"actual/file/path\"," +
            "\"line\":0," +
            "\"description\":\"Explain the issue\"," +
            "\"recommendation\":\"Explain the fix\"}]}\n\n" +

            "If there are no real issues return:\n" +
            "{\"findings\":[]}\n\n" +

            "SOURCE FILES:\n\n" +
            files;
    }
}

public sealed class CodeReviewFinding
{
    public string Severity { get; set; } = "MEDIUM";

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string File { get; set; } = string.Empty;

    public int Line { get; set; }

    public string Description { get; set; } = string.Empty;

    public string Recommendation { get; set; } = string.Empty;
}