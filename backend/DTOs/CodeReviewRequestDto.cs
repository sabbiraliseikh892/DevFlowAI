namespace DevFlowAI.API.DTOs;

public sealed class CodeReviewRequestDto
{
    public string RepositoryUrl { get; set; } = string.Empty;

    public string Branch { get; set; } = "main";
}