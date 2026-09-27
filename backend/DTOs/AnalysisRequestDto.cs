namespace DevFlowAI.API.DTOs;

public record AnalysisRequestDto(
    string RepositoryUrl,
    string Branch = "main"
);
